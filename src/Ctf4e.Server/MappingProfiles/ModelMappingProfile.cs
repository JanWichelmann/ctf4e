﻿using AutoMapper;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.MappingProfiles
{
    public class ModelMappingProfile : Profile
    {
        public ModelMappingProfile()
        {
            CreateMap<UserEntity, User>()
                .ForMember(u => u.Group, opt => opt.ExplicitExpansion())
                .ForMember(u => u.FlagSubmissions, opt => opt.ExplicitExpansion())
                .ForMember(u => u.ExerciseSubmissions, opt => opt.ExplicitExpansion());

            CreateMap<GroupEntity, Group>()
                .ForMember(g => g.Slot, opt => opt.ExplicitExpansion())
                .ForMember(g => g.Members, opt => opt.ExplicitExpansion())
                .ForMember(g => g.LessonExecutions, opt => opt.ExplicitExpansion());

            CreateMap<LessonEntity, Lesson>()
                .ForMember(l => l.Executions, opt => opt.ExplicitExpansion())
                .ForMember(l => l.Flags, opt => opt.ExplicitExpansion())
                .ForMember(l => l.Exercises, opt => opt.ExplicitExpansion());

            CreateMap<SlotEntity, Slot>()
                .ForMember(s => s.Groups, opt => opt.ExplicitExpansion());

            CreateMap<LessonExecutionEntity, LessonExecution>()
                .ForMember(l => l.Group, opt => opt.ExplicitExpansion())
                .ForMember(l => l.Lesson, opt => opt.ExplicitExpansion());

            CreateMap<FlagEntity, Flag>()
                .ForMember(f => f.Lesson, opt => opt.ExplicitExpansion())
                .ForMember(f => f.Submissions, opt => opt.ExplicitExpansion());

            CreateMap<FlagSubmissionEntity, FlagSubmission>()
                .ForMember(f => f.User, opt => opt.ExplicitExpansion())
                .ForMember(f => f.Flag, opt => opt.ExplicitExpansion());

            CreateMap<ExerciseEntity, Exercise>()
                .ForMember(e => e.Lesson, opt => opt.ExplicitExpansion())
                .ForMember(e => e.Submissions, opt => opt.ExplicitExpansion());

            CreateMap<ExerciseSubmissionEntity, ExerciseSubmission>()
                .ForMember(e => e.User, opt => opt.ExplicitExpansion())
                .ForMember(e => e.Exercise, opt => opt.ExplicitExpansion());
        }
    }
}