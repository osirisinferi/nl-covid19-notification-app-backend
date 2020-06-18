// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System.Linq;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ResourceBundle
{
    public class SafeGetResourceBundleCommand
    {
        private readonly ExposureContentDbContext _DbConfig;

        public SafeGetResourceBundleCommand(ExposureContentDbContext dbConfig)
        {
            _DbConfig = dbConfig;
        }

        public ResourceBundleContentEntity Execute(string id)
        {
            return _DbConfig.Set<ResourceBundleContentEntity>()
                .Where(x => x.PublishingId == id)
                .Take(1)
                .SingleOrDefault();
        }
    }
}