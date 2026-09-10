using AutoMapper;
using Balenthiran.Snipit.DomainModels.Models;
using Balenthiran.Snipit.EntityModels;
using Balenthiran.Snipit.Services.Cutting;
using Balenthiran.Snipit.Services.Transcription;

namespace Balenthiran.Snipit.Services;

/// <summary>Entity ↔ DomainModel mappings (the DB boundary). Flat columns map by name;
/// the JSON-serialised aggregate columns (transcript, keep-ranges) are rehydrated via
/// their dedicated serializers.</summary>
public class Mapper : Profile
{
    public Mapper()
    {
        CreateMap<TranscriptionJobEntity, DomainTranscriptionJob>()
            .ForMember(d => d.Transcript, o => o.MapFrom(s => TranscriptJsonSerializer.Deserialize(s.TranscriptJson)));

        CreateMap<CutJobEntity, DomainCutJob>()
            .ForMember(d => d.KeepRanges, o => o.MapFrom(s => KeepRangeJsonSerializer.Deserialize(s.KeepRangesJson)));
    }
}
