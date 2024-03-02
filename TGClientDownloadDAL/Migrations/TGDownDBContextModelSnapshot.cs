﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TGClientDownloadDAL;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    [DbContext(typeof(TGDownDBContext))]
    partial class TGDownDBContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TGClientDownloadDAL.Entities.ConfigurationParameter", b =>
                {
                    b.Property<int>("ConfigurationParameterId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("ConfigurationParameterId"));

                    b.Property<string>("ParameterName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("ParameterType")
                        .HasColumnType("integer");

                    b.Property<string>("ParameterValue")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("ConfigurationParameterId");

                    b.HasIndex("ParameterName")
                        .IsUnique();

                    b.ToTable("system_ConfigParam");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.ScheduledTask", b =>
                {
                    b.Property<int>("ScheduledTaskId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("ScheduledTaskId"));

                    b.Property<bool>("Enabled")
                        .HasColumnType("boolean");

                    b.Property<int>("Interval")
                        .HasColumnType("integer");

                    b.Property<bool>("IsRunning")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("LastFinish")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("LastStart")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("TasksName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("ScheduledTaskId");

                    b.ToTable("ScheduledTask");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramChat", b =>
                {
                    b.Property<int>("TelegramChatId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("TelegramChatId"));

                    b.Property<long>("AccessHash")
                        .HasColumnType("bigint");

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.HasKey("TelegramChatId");

                    b.HasIndex("ChatId", "AccessHash")
                        .IsUnique();

                    b.ToTable("TelegramChat");

                    b.UseTptMappingStrategy();
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramFile", b =>
                {
                    b.Property<int>("TelegramFileId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("TelegramFileId"));

                    b.Property<long>("AccessHash")
                        .HasColumnType("bigint");

                    b.Property<long>("FileId")
                        .HasColumnType("bigint");

                    b.Property<int>("TelegramMessageId")
                        .HasColumnType("integer");

                    b.HasKey("TelegramFileId");

                    b.HasIndex("TelegramMessageId")
                        .IsUnique();

                    b.HasIndex("FileId", "AccessHash")
                        .IsUnique();

                    b.ToTable("TelegramFile");

                    b.UseTptMappingStrategy();
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramMessage", b =>
                {
                    b.Property<int>("TelegramMessageId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("TelegramMessageId"));

                    b.Property<int>("MessageId")
                        .HasColumnType("integer");

                    b.HasKey("TelegramMessageId");

                    b.ToTable("TelegramMessage");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramChannel", b =>
                {
                    b.HasBaseType("TGClientDownloadDAL.Entities.TelegramChat");

                    b.Property<bool>("AutoDownloadEnabled")
                        .HasColumnType("boolean");

                    b.Property<string>("ChannelName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("FileNameTemplate")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.ToTable("TelegramChannel");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramMediaDocument", b =>
                {
                    b.HasBaseType("TGClientDownloadDAL.Entities.TelegramFile");

                    b.Property<long>("DataTransmitted")
                        .HasColumnType("bigint");

                    b.Property<int>("DownloadStatus")
                        .HasColumnType("integer");

                    b.Property<int?>("ErrorType")
                        .HasColumnType("integer");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("LastUpdate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("Size")
                        .HasColumnType("bigint");

                    b.Property<int>("SourceChatId")
                        .HasColumnType("integer");

                    b.HasIndex("SourceChatId");

                    b.ToTable("TelegramMediaDocument");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramFile", b =>
                {
                    b.HasOne("TGClientDownloadDAL.Entities.TelegramMessage", "TelegramMessage")
                        .WithOne("Document")
                        .HasForeignKey("TGClientDownloadDAL.Entities.TelegramFile", "TelegramMessageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("TelegramMessage");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramChannel", b =>
                {
                    b.HasOne("TGClientDownloadDAL.Entities.TelegramChat", null)
                        .WithOne()
                        .HasForeignKey("TGClientDownloadDAL.Entities.TelegramChannel", "TelegramChatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramMediaDocument", b =>
                {
                    b.HasOne("TGClientDownloadDAL.Entities.TelegramChat", "SourceChat")
                        .WithMany("MediaDocuments")
                        .HasForeignKey("SourceChatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TGClientDownloadDAL.Entities.TelegramFile", null)
                        .WithOne()
                        .HasForeignKey("TGClientDownloadDAL.Entities.TelegramMediaDocument", "TelegramFileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("SourceChat");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramChat", b =>
                {
                    b.Navigation("MediaDocuments");
                });

            modelBuilder.Entity("TGClientDownloadDAL.Entities.TelegramMessage", b =>
                {
                    b.Navigation("Document");
                });
#pragma warning restore 612, 618
        }
    }
}
