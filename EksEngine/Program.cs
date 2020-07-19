﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ConsoleApps;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.DevOps;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ExposureKeySetsEngine;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ExposureKeySetsEngine.ContentFormatters;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ExposureKeySetsEngine.FormatV1;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Manifest;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ProtocolSettings;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services.Signing.Configs;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services.Signing.Providers;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services.Signing.Signers;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.EksEngine
{
    class Program
    {
        static void Main(string[] args)
        {
            new ConsoleAppRunner().Execute(args, Configure, Start);
        }

        private static void Start(IServiceProvider arg1, string[] arg2)
        {
        }

        private static void Configure(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddScoped(x =>
            {
                var config = new StandardEfDbConfig(configuration, "Content");
                var builder = new SqlServerDbContextOptionsBuilder(config);
                var result = new ContentDbContext(builder.Build());
                return result;
            });

            services.AddSingleton<IUtcDateTimeProvider, StandardUtcDateTimeProvider>();
            services.AddScoped(x =>
                new ExposureKeySetBatchJobMk2(
                    x.GetRequiredService<IGaenContentConfig>(),
                    x.GetRequiredService<IExposureKeySetBuilder>(),
                    x.GetRequiredService<WorkflowDbContext>(),
                    x.GetRequiredService<ContentDbContext>(),
                    x.GetRequiredService<IUtcDateTimeProvider>(),
                    x.GetRequiredService<IPublishingId>(),
                    x.GetRequiredService<ILogger<ExposureKeySetBatchJobMk2>>()
                ));

            services.AddSingleton<IGaenContentConfig, StandardGaenContentConfig>();
            services.AddScoped<IExposureKeySetBuilder>(x =>
                new ExposureKeySetBuilderV1(
                    x.GetRequiredService<IExposureKeySetHeaderInfoConfig>(),
                    new EcdSaSigner(new X509CertificateProvider(new CertificateProviderConfig(x.GetRequiredService<IConfiguration>(), "Signing:GA"), x.GetRequiredService<ILogger<X509CertificateProvider>>())),
                    new CmsSigner(new X509CertificateProvider(new CertificateProviderConfig(x.GetRequiredService<IConfiguration>(), "Signing:NL"), x.GetRequiredService<ILogger<X509CertificateProvider>>())),
                    x.GetRequiredService<IUtcDateTimeProvider>(), //TODO pass in time thru execute
                    new GeneratedProtobufContentFormatter(),
                    x.GetRequiredService<ILogger<ExposureKeySetBuilderV1>>()
                ));

            services.AddScoped<IExposureKeySetHeaderInfoConfig, ExposureKeySetHeaderInfoConfig>();
            services.AddScoped<IPublishingId, StandardPublishingIdFormatter>();

        }
    }
}
