/**
 * Known-bad implementations for the #22 change: break one rule, prove the suite goes RED.
 *
 * A test written alongside the code it covers has never been shown to fail — the code was
 * already correct when it was written. These are the five ways this change silently reverts,
 * each of which leaves every other assertion green.
 *
 * Guards: the find-string must match EXACTLY ONCE (a bad anchor is a harness failure, not a
 * finding against innocent code), and every file is restored in a finally block.
 */
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');

const ROOT = '/data/repos/snip-it';

const MUTATIONS = [
  {
    name: '1. the export survives its own download (read-once → ordinary open)',
    file: 'backend/Balenthiran.Snipit.Services/Infrastructure/LocalDiskFileStorageService.cs',
    find: 'FileOptions.DeleteOnClose | FileOptions.Asynchronous',
    replace: 'FileOptions.Asynchronous',
    suite: 'dotnet',
    filter: 'LocalDiskFileStorageServiceTests',
  },
  {
    name: '2. the transcribed upload is never deleted',
    file: 'backend/Balenthiran.Snipit.Services/Transcription/TranscriptionJobProcessor.cs',
    find: '            fileStorage.Delete(entity.SourceFilePath);\n',
    replace: '',
    suite: 'dotnet',
    filter: 'TranscriptionJobProcessorTests',
  },
  {
    name: '3. the re-sent cut source is never deleted',
    file: 'backend/Balenthiran.Snipit.Services/Cutting/CutJobProcessor.cs',
    find: '            fileStorage.Delete(entity.SourceFilePath);\n',
    replace: '',
    suite: 'dotnet',
    filter: 'CutJobProcessorTests',
  },
  {
    name: '4. the cut reads the transcription job\'s deleted path again',
    file: 'backend/Balenthiran.Snipit.Services/Cutting/CutService.cs',
    find: 'SourceFilePath = sourceStorageKey,',
    replace: 'SourceFilePath = "uploads/x.mp4",',
    suite: 'dotnet',
    filter: 'CutServiceTests',
  },
  {
    // CONTROL. Five mutations all reporting KILLED is the direction that flatters me, so one of
    // them must be a change the suite genuinely cannot see. If this reports KILLED, the harness
    // is broken (or the suite is asserting something it has no business asserting) and the other
    // four results are worthless.
    name: 'CONTROL — a semantically invisible change (buffer size) MUST survive',
    file: 'backend/Balenthiran.Snipit.Services/Infrastructure/LocalDiskFileStorageService.cs',
    find: 'bufferSize: 4096',
    replace: 'bufferSize: 8192',
    suite: 'dotnet',
    filter: 'LocalDiskFileStorageServiceTests',
    expect: 'SURVIVED',
  },
  {
    name: '5. the editor goes back to streaming the video from the server',
    file: 'frontend/src/features/transcript-editor/components/TranscriptEditorPage.tsx',
    find: 'src={videoUrl ?? undefined}',
    replace: 'src={`/snipit/api/transcriptions/${transcriptionJobId}/source`}',
    suite: 'e2e',
    filter: 'uploading a file transcribes it',
  },
];

function run(m) {
  const path = `${ROOT}/${m.file}`;
  const original = fs.readFileSync(path, 'utf8');

  const occurrences = original.split(m.find).length - 1;
  if (occurrences !== 1) {
    return { name: m.name, verdict: 'HARNESS ERROR', detail: `anchor matched ${occurrences} times, expected exactly 1` };
  }

  try {
    fs.writeFileSync(path, original.replace(m.find, m.replace));

    const cmd = m.suite === 'dotnet'
      ? ['dotnet', ['test', '--nologo', '--filter', `FullyQualifiedName~${m.filter}`], `${ROOT}/backend`]
      : ['npx', ['playwright', 'test', '-g', m.filter], `${ROOT}/frontend`];

    try {
      execFileSync(cmd[0], cmd[1], { cwd: cmd[2], encoding: 'utf8', stdio: 'pipe' });
      return { name: m.name, verdict: 'SURVIVED', detail: 'the suite stayed GREEN over a known-bad implementation' };
    } catch {
      return { name: m.name, verdict: 'KILLED', detail: 'suite went RED, as it must' };
    }
  } finally {
    fs.writeFileSync(path, original);
  }
}

const results = MUTATIONS.map((m) => {
  const r = run(m);
  const expected = m.expect ?? 'KILLED';
  r.ok = r.verdict === expected;
  console.log(`${r.ok ? ' ✓' : ' ✗'} ${r.verdict.padEnd(13)} ${r.name}\n                 ${r.detail}`);
  return r;
});

const killed = results.filter((r) => r.verdict === 'KILLED').length;
const failures = results.filter((r) => !r.ok);
console.log(`\n${killed}/${MUTATIONS.filter((m) => (m.expect ?? 'KILLED') === 'KILLED').length} known-bad implementations killed; control behaved as required: ${!failures.some((f) => f.name.startsWith('CONTROL'))}`);
process.exit(failures.length === 0 ? 0 : 1);
