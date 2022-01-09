﻿// <auto-generated />
using System;
using Ctf4e.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Ctf4e.Server.Migrations
{
    [DbContext(typeof(CtfDbContext))]
    [Migration("20220109102119_AddUserPasswordHash")]
    partial class AddUserPasswordHash
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ConfigurationItemEntity", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Value")
                        .HasColumnType("longtext");

                    b.HasKey("Key");

                    b.ToTable("ConfigurationItems");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ExerciseEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("BasePoints")
                        .HasColumnType("int");

                    b.Property<int>("ExerciseNumber")
                        .HasColumnType("int");

                    b.Property<bool>("IsMandatory")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsPreStartAvailable")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int>("PenaltyPoints")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("LabId");

                    b.ToTable("Exercises");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ExerciseSubmissionEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("ExerciseId")
                        .HasColumnType("int");

                    b.Property<bool>("ExercisePassed")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime>("SubmissionTime")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<int>("Weight")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ExerciseId");

                    b.HasIndex("UserId");

                    b.ToTable("ExerciseSubmissions");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.FlagEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("BasePoints")
                        .HasColumnType("int");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<bool>("IsBounty")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("LabId");

                    b.ToTable("Flags");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.FlagSubmissionEntity", b =>
                {
                    b.Property<int>("FlagId")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<DateTime>("SubmissionTime")
                        .HasColumnType("datetime(6)");

                    b.HasKey("FlagId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("FlagSubmissions");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.GroupEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("ScoreboardAnnotation")
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("ScoreboardAnnotationHoverText")
                        .HasMaxLength(200)
                        .HasColumnType("varchar(200)");

                    b.Property<bool>("ShowInScoreboard")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("SlotId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("SlotId");

                    b.ToTable("Groups");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.LabEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ApiCode")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int>("MaxFlagPoints")
                        .HasColumnType("int");

                    b.Property<int>("MaxPoints")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("ServerBaseUrl")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<bool>("Visible")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("Id");

                    b.ToTable("Labs");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.LabExecutionEntity", b =>
                {
                    b.Property<int>("GroupId")
                        .HasColumnType("int");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.Property<DateTime>("End")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime>("PreStart")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime>("Start")
                        .HasColumnType("datetime(6)");

                    b.HasKey("GroupId", "LabId");

                    b.HasIndex("LabId");

                    b.ToTable("LabExecutions");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.SlotEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.HasKey("Id");

                    b.ToTable("Slots");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.UserEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("GroupFindingCode")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int?>("GroupId")
                        .HasColumnType("int");

                    b.Property<bool>("IsTutor")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("MoodleName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int>("MoodleUserId")
                        .HasColumnType("int");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("varchar(200)");

                    b.Property<int>("Privileges")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.HasIndex("MoodleUserId")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ExerciseEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.LabEntity", "Lab")
                        .WithMany("Exercises")
                        .HasForeignKey("LabId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Lab");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ExerciseSubmissionEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.ExerciseEntity", "Exercise")
                        .WithMany("Submissions")
                        .HasForeignKey("ExerciseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Ctf4e.Server.Data.Entities.UserEntity", "User")
                        .WithMany("ExerciseSubmissions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Exercise");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.FlagEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.LabEntity", "Lab")
                        .WithMany("Flags")
                        .HasForeignKey("LabId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Lab");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.FlagSubmissionEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.FlagEntity", "Flag")
                        .WithMany("Submissions")
                        .HasForeignKey("FlagId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Ctf4e.Server.Data.Entities.UserEntity", "User")
                        .WithMany("FlagSubmissions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Flag");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.GroupEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.SlotEntity", "Slot")
                        .WithMany("Groups")
                        .HasForeignKey("SlotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Slot");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.LabExecutionEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.GroupEntity", "Group")
                        .WithMany("LabExecutions")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Ctf4e.Server.Data.Entities.LabEntity", "Lab")
                        .WithMany("Executions")
                        .HasForeignKey("LabId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Group");

                    b.Navigation("Lab");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.UserEntity", b =>
                {
                    b.HasOne("Ctf4e.Server.Data.Entities.GroupEntity", "Group")
                        .WithMany("Members")
                        .HasForeignKey("GroupId");

                    b.Navigation("Group");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.ExerciseEntity", b =>
                {
                    b.Navigation("Submissions");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.FlagEntity", b =>
                {
                    b.Navigation("Submissions");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.GroupEntity", b =>
                {
                    b.Navigation("LabExecutions");

                    b.Navigation("Members");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.LabEntity", b =>
                {
                    b.Navigation("Executions");

                    b.Navigation("Exercises");

                    b.Navigation("Flags");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.SlotEntity", b =>
                {
                    b.Navigation("Groups");
                });

            modelBuilder.Entity("Ctf4e.Server.Data.Entities.UserEntity", b =>
                {
                    b.Navigation("ExerciseSubmissions");

                    b.Navigation("FlagSubmissions");
                });
#pragma warning restore 612, 618
        }
    }
}
