using AutoMapper;
using Balenthiran.Snipit.Abstractions.DomainModels;
using Balenthiran.Snipit.DataModels.Models;

namespace Balenthiran.Snipit.WebApi;

/// <summary>DataModel ↔ DomainModel mappings (the API boundary). The Transcript aggregate is
/// assembled directly in TranscriptionRoutes since it merges fields from two sources (job id + transcript).</summary>
public class Mapper : Profile
{
    public Mapper()
    {
        CreateMap<IDomainTranscriptionJob, TranscriptionJobDto>();
        CreateMap<IDomainCutJob, CutJobDto>();
        CreateMap<IDomainCutJob, CutJobResponse>();
        CreateMap<DomainTranscriptSegment, TranscriptSegmentDto>();
        CreateMap<DomainTranscriptWord, TranscriptWordDto>().ReverseMap();
    }
}
