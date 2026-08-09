import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { configureStore } from '@reduxjs/toolkit';
import { Provider } from 'react-redux';
import { emptySplitApi } from '../api/emptyApi';
import editorReducer, { type EditorState } from '../features/transcript-editor/editorSlice';

/** Same reducer shape as the real app store (src/store/index.ts), so
 * components under test see exactly the RootState they're typed against. */
export function createTestStore(transcriptEditorState?: Partial<EditorState>) {
  const store = configureStore({
    reducer: {
      [emptySplitApi.reducerPath]: emptySplitApi.reducer,
      transcriptEditor: editorReducer,
    },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(emptySplitApi.middleware),
    preloadedState: transcriptEditorState
      ? {
          transcriptEditor: {
            transcriptId: null,
            durationSeconds: 0,
            words: [],
            segments: [],
            bufferSeconds: 0.2,
            ...transcriptEditorState,
          },
        }
      : undefined,
  });
  return store;
}

export function renderWithStore(ui: ReactElement, transcriptEditorState?: Partial<EditorState>) {
  const store = createTestStore(transcriptEditorState);
  return { store, ...render(<Provider store={store}>{ui}</Provider>) };
}
