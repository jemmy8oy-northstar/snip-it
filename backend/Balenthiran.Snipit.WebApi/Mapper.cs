using AutoMapper;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DataModels.Models;
using Balenthiran.Snipit.DomainModels.Models;

namespace Balenthiran.Snipit.WebApi;

/// <summary>DataModel ↔ DomainModel mappings (the API boundary). The Transcript aggregate is
/// assembled directly in TranscriptionRoutes since it merges fields from two sources (job id + transcript).</summary>
public class Mapper : Profile
{
    public Mapper()
    {
        CreateMap<IDomainTranscriptionJob, TranscriptionJob>();
        CreateMap<IDomainCutJob, CutJob>();
        CreateMap<IDomainCutJob, CutJobResponse>();
        CreateMap<IDomainTranscriptSegment, TranscriptSegment>();
        CreateMap<IDomainTranscriptWord, TranscriptWord>();
        CreateMap<TranscriptWord, DomainTranscriptWord>();
    }
}
