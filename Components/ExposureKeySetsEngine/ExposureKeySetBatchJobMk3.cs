﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using EFCore.BulkExtensions;
using Microsoft.Extensions.Logging;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Entities;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ExposureKeySetsEngine.FormatV1;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Framework;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ProtocolSettings;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Workflow;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ExposureKeySetsEngine
{
    /// <summary>
    /// Add database IO to the job
    /// </summary>
    public sealed class ExposureKeySetBatchJobMk3 : IDisposable
    {
        private bool _Disposed;
        private readonly string _JobName;

        //private readonly IExposureKeySetBatchJobConfig _JobConfig;
        private readonly IEksConfig _EksConfig;

        //private readonly IExposureKeySetWriter _Writer;
        private readonly IEksBuilder _SetBuilder;
        private readonly DateTime _Start;

        private readonly IEksStuffingGenerator _EksStuffingGenerator;

        private int _Counter;
        private readonly List<EksCreateJobInputEntity> _Used;
        private readonly List<TemporaryExposureKeyArgs> _KeyBatch = new List<TemporaryExposureKeyArgs>();

        private readonly WorkflowDbContext _WorkflowDbContext;
        private readonly PublishingJobDbContext _PublishingDbContext;
        private readonly ContentDbContext _ContentDbContext;

        private readonly IPublishingIdService _PublishingIdService;
        private readonly ILogger _Logger;

        private readonly ITransmissionRiskLevelCalculation _TransmissionRiskLevelCalculation;

        public ExposureKeySetBatchJobMk3(IEksConfig eksConfig, IEksBuilder builder, WorkflowDbContext workflowDbContext, PublishingJobDbContext publishingDbContext, ContentDbContext contentDbContext, IUtcDateTimeProvider dateTimeProvider, IPublishingIdService publishingIdService, ILogger<ExposureKeySetBatchJobMk3> logger, ITransmissionRiskLevelCalculation transmissionRiskLevelCalculation, IEksStuffingGenerator eksStuffingGenerator)
        {
            //_JobConfig = jobConfig;
            _EksConfig = eksConfig ?? throw new ArgumentNullException(nameof(eksConfig));
            _SetBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
            _WorkflowDbContext = workflowDbContext ?? throw new ArgumentNullException(nameof(workflowDbContext));
            _PublishingDbContext = publishingDbContext ?? throw new ArgumentNullException(nameof(publishingDbContext));
            _ContentDbContext = contentDbContext ?? throw new ArgumentNullException(nameof(contentDbContext));
            _TransmissionRiskLevelCalculation = transmissionRiskLevelCalculation ?? throw new ArgumentNullException(nameof(transmissionRiskLevelCalculation));
            _EksStuffingGenerator = eksStuffingGenerator ?? throw new ArgumentNullException(nameof(eksStuffingGenerator));
            _PublishingIdService = publishingIdService;
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _Used = new List<EksCreateJobInputEntity>(_EksConfig.TekCountMax); //
            _Start = dateTimeProvider.Now();
            _JobName = $"ExposureKeySetsJob_{_Start:u}".Replace(" ", "_").Replace(":", "_");
        }

        public async Task Execute()
        {
            if (_Disposed)
                throw new ObjectDisposedException(_JobName);

            _Logger.LogInformation("Started - JobName:{_JobName}", _JobName);

            if (!WindowsIdentityStuff.CurrentUserIsAdministrator()) //TODO remove warning when UAC is not in play
                _Logger.LogWarning("{JobName} started WITHOUT elevated privileges - errors may occur when signing content.", _JobName);

            _WorkflowDbContext.EnsureNoChangesOrTransaction();
            _PublishingDbContext.EnsureNoChangesOrTransaction();
            _ContentDbContext.EnsureNoChangesOrTransaction();

            await CopyInput();
            await Stuff();
            await BuildBatches();
            await CommitResults();

            _Logger.LogInformation("{JobName} complete.", _JobName);
        }

        private async Task Stuff()
        {
            var tekCount = _PublishingDbContext.Set<EksCreateJobInputEntity>().Count(x => x.TransmissionRiskLevel != TransmissionRiskLevel.None);

            if (tekCount == 0)
                return;
            
            
            var stuffingCount = tekCount < _EksConfig.TekCountMin ? _EksConfig.TekCountMin - tekCount : 0;
            if (stuffingCount == 0)
                return;

            var stuffing = _EksStuffingGenerator.Execute(new StuffingArgs {Count = stuffingCount, JobTime = _Start});

            _PublishingDbContext.BeginTransaction();
            await _PublishingDbContext.Set<EksCreateJobInputEntity>().AddRangeAsync(stuffing);
            _PublishingDbContext.SaveAndCommit();
        }

        private async Task BuildBatches()
        {
            _Logger.LogDebug("Build batches.");

            const int size = 100;
            var count = 0;
            var keys = GetInputBatch(count, size); //TODO page or otherwise close the data reader before writing in Build

            while (keys.Length > 0)
            {
                if (_KeyBatch.Count + keys.Length > _EksConfig.TekCountMax)
                    await Build();

                _KeyBatch.AddRange(keys.Select(Map));
                _Used.AddRange(keys);

                count += size;
                keys = GetInputBatch(count, size);
            }

            if (_KeyBatch.Count > 0)
                await Build();
        }

        private static TemporaryExposureKeyArgs Map(EksCreateJobInputEntity c)
            => new TemporaryExposureKeyArgs 
            { 
                RollingPeriod = c.RollingPeriod,
                TransmissionRiskLevel = c.TransmissionRiskLevel,
                KeyData = c.KeyData,
                RollingStartNumber = c.RollingStartNumber
            };

        private async Task Build()
        {
            _Logger.LogDebug("Build EKS.");

            var args = _KeyBatch.ToArray();
            
            var content = await _SetBuilder.BuildAsync(args);
            var e = new EksCreateJobOutputEntity
            {
                Region = DefaultValues.Region,
                Release = _Start,
                CreatingJobName = _JobName,
                CreatingJobQualifier = ++_Counter,
                Content = content, 
            };

            _KeyBatch.Clear();

            await WriteOutput(e);
            await WriteUsed(_Used.ToArray()); 
        }

        private async Task WriteOutput(EksCreateJobOutputEntity e)
        {
            _Logger.LogInformation("Write EKS {CreatingJobQualifier}.", e.CreatingJobQualifier);

            await using (_PublishingDbContext.BeginTransaction())
            {
                await _PublishingDbContext.AddAsync(e);
                _PublishingDbContext.SaveAndCommit();
            }
        }

        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            //TODO _JobDatabase?.Dispose();
        }

        private async Task CopyInput()
        {
            _Logger.LogDebug("Copy input TEKs.");

            await using (_PublishingDbContext.BeginTransaction())
            {
                await _PublishingDbContext.Set<EksCreateJobInputEntity>().BatchDeleteAsync(); //TODO truncate instead
                await _PublishingDbContext.Set<EksCreateJobOutputEntity>().BatchDeleteAsync();
                _PublishingDbContext.SaveAndCommit();
            }

            await using (_WorkflowDbContext.BeginTransaction())
            {
                var read = _WorkflowDbContext.TemporaryExposureKeys
                    .Where(x => (x.Owner.AuthorisedByCaregiver!=null) 
                                && x.Owner.DateOfSymptomsOnset != null
                                && x.PublishingState == PublishingState.Unpublished 
                                && x.PublishAfter <= _Start
                                )
                    .Select(x => new { Tek = x, DateOfSymptomsOnset = x.Owner.DateOfSymptomsOnset.Value  })
                    .Select(x => new EksCreateJobInputEntity
                    {
                        TekId = x.Tek.Id,
                        RollingPeriod = x.Tek.RollingPeriod,
                        KeyData = x.Tek.KeyData,
                        TransmissionRiskLevel = _TransmissionRiskLevelCalculation.Calculate(x.Tek.RollingStartNumber, x.DateOfSymptomsOnset),
                        RollingStartNumber = x.Tek.RollingStartNumber,
                    }).ToList();

                if (read.Count == 0)
                    return;

                await using (_PublishingDbContext.BeginTransaction())
                {
                    await _PublishingDbContext.BulkInsertAsync(read.ToList());
                    _PublishingDbContext.SaveAndCommit();
                }
            }
        }

        private EksCreateJobInputEntity[] GetInputBatch(int skip, int take)
        {
            _Logger.LogDebug("Read input batch - skip {Skip}, take {Take}.", skip, take);
            return _PublishingDbContext.Set<EksCreateJobInputEntity>()
                .Where(x => x.TransmissionRiskLevel != TransmissionRiskLevel.None)
                .OrderBy(x => x.KeyData).Skip(skip).Take(take).ToArray();
        }

        private async Task WriteUsed(EksCreateJobInputEntity[] used)
        {
            _Logger.LogDebug("Mark used, count {Length}.", used.Length);

            foreach (var i in used)
            {
                i.Used = true;
            }
            await using (_PublishingDbContext.BeginTransaction())
            {
                await _PublishingDbContext.BulkUpdateAsync(used);
                _PublishingDbContext.SaveAndCommit();
            }
        }

        private async Task CommitResults()
        {
            _Logger.LogInformation("Commit results - publish EKSs.");

            await using (_PublishingDbContext.BeginTransaction())
            {
                var move = _PublishingDbContext.Set<EksCreateJobOutputEntity>().Select(
                    x => new ContentEntity
                    {
                        Created = _Start,
                        Release = x.Release,
                        ContentTypeName = MediaTypeNames.Application.Zip,
                        Content = x.Content,
                        Type = ContentTypes.ExposureKeySet,
                        //CreatingJobName = x.CreatingJobName,
                        //CreatingJobQualifier = x.CreatingJobQualifier,
                        PublishingId = _PublishingIdService.Create(x.Content)
                    }).ToArray();

                await using (_ContentDbContext.BeginTransaction())
                {
                    _ContentDbContext.Set<ContentEntity>().AddRange(move);
                    _ContentDbContext.SaveAndCommit();
                }
            }

            _Logger.LogInformation("Commit results - Mark TEKs as Published.");

            await using (_PublishingDbContext.BeginTransaction()) //Read-only
            {
                await using (_WorkflowDbContext.BeginTransaction())
                {
                    var count = 0;
                    var used = _PublishingDbContext.Set<EksCreateJobInputEntity>()
                        .Where(x => x.Used && x.TekId != null)
                        .Skip(count)
                        .Select(x => x.TekId.Value)
                        .Take(100)
                        .ToArray();

                    while (used.Length > 0)
                    {
                        var zap = _WorkflowDbContext.TemporaryExposureKeys
                            .Where(x => used.Contains(x.Id))
                            .ToList();

                        foreach (var i in zap)
                        {
                            i.PublishingState = PublishingState.Published;
                        }

                        await _WorkflowDbContext.BulkUpdateAsync(zap, x => x.PropertiesToInclude = new List<string> {nameof(TekEntity.PublishingState)});

                        count += used.Length;

                        used = _PublishingDbContext.Set<EksCreateJobInputEntity>()
                            .Where(x => x.Used)
                            .Skip(count)
                            .Select(x => x.Id)
                            .Take(100)
                            .ToArray();
                    }

                    _WorkflowDbContext.SaveAndCommit();
                }

                _Logger.LogInformation("Cleanup job tables.");
                await _PublishingDbContext.Set<EksCreateJobInputEntity>().BatchDeleteAsync();
                await _PublishingDbContext.Set<EksCreateJobOutputEntity>().BatchDeleteAsync();
                _PublishingDbContext.SaveAndCommit();
            }
        }
    }
}