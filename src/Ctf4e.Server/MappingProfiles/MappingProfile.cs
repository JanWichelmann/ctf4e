using AutoMapper;
using Ctf4e.Server.Controllers;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;
using Ctf4e.Server.Services;

namespace Ctf4e.Server.MappingProfiles;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Generic entity <-> model mappings
        
        CreateMap<UserEntity, User>()
            .ForMember(u => u.Group, opt => opt.ExplicitExpansion())
            .ForMember(u => u.FlagSubmissions, opt => opt.ExplicitExpansion())
            .ForMember(u => u.ExerciseSubmissions, opt => opt.ExplicitExpansion());
        CreateMap<User, UserEntity>();

        CreateMap<GroupEntity, Group>()
            .ForMember(g => g.Slot, opt => opt.ExplicitExpansion())
            .ForMember(g => g.Members, opt => opt.ExplicitExpansion())
            .ForMember(g => g.LabExecutions, opt => opt.ExplicitExpansion());
        CreateMap<Group, GroupEntity>();

        CreateMap<LabEntity, Lab>()
            .ForMember(l => l.Executions, opt => opt.ExplicitExpansion())
            .ForMember(l => l.Flags, opt => opt.ExplicitExpansion())
            .ForMember(l => l.Exercises, opt => opt.ExplicitExpansion());
        CreateMap<Lab, LabEntity>();

        CreateMap<SlotEntity, Slot>()
            .ForMember(s => s.Groups, opt => opt.ExplicitExpansion());
        CreateMap<Slot, SlotEntity>();

        CreateMap<LabExecutionEntity, LabExecution>()
            .ForMember(l => l.Group, opt => opt.ExplicitExpansion())
            .ForMember(l => l.Lab, opt => opt.ExplicitExpansion());
        CreateMap<LabExecution, LabExecutionEntity>();

        CreateMap<FlagEntity, Flag>()
            .ForMember(f => f.Lab, opt => opt.ExplicitExpansion())
            .ForMember(f => f.Submissions, opt => opt.ExplicitExpansion());
        CreateMap<Flag, FlagEntity>();

        CreateMap<FlagSubmissionEntity, FlagSubmission>()
            .ForMember(f => f.User, opt => opt.ExplicitExpansion())
            .ForMember(f => f.Flag, opt => opt.ExplicitExpansion());
        CreateMap<FlagSubmission, FlagSubmissionEntity>();

        CreateMap<ExerciseEntity, Exercise>()
            .ForMember(e => e.Lab, opt => opt.ExplicitExpansion())
            .ForMember(e => e.Submissions, opt => opt.ExplicitExpansion());
        CreateMap<Exercise, ExerciseEntity>();

        CreateMap<ExerciseSubmissionEntity, ExerciseSubmission>()
            .ForMember(e => e.User, opt => opt.ExplicitExpansion())
            .ForMember(e => e.Exercise, opt => opt.ExplicitExpansion());
        CreateMap<ExerciseSubmission, ExerciseSubmissionEntity>();
        
        // Other mappings
        ExerciseService.RegisterMappings(this);
        FlagService.RegisterMappings(this);
        GroupService.RegisterMappings(this);
        LabService.RegisterMappings(this);
        SlotService.RegisterMappings(this);
        AdminExercisesController.RegisterMappings(this);
        AdminFlagsController.RegisterMappings(this);
        AdminGroupsController.RegisterMappings(this);
        AdminLabsController.RegisterMappings(this);
        AdminSlotsController.RegisterMappings(this);
    }
}