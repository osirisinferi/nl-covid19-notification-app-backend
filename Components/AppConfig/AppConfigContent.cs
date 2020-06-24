﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.AppConfig
{
    public class AppConfigContent
    {
        /// <summary>
        /// The minimum version of the app.The app has a hardcoded version number that is increased by 1  on each app release. Whenever the app downloads the manifest, it must compare its hardcoded version  number with that of the manifest. If the hardcoded version number is less than the manifest value, the user will be asked to upgrade the app from the app store.See https://github.com/minvws/nl-covid19-notification-app-coordination/blob/master/architecture/Solution%20Architecture.md#lifecycle-management.
        /// </summary>
        [JsonProperty("version")]
        public long Version { get; set; }

        ///This defines the period for retrieving the manifest, in minutes.
        [JsonProperty("manifestFrequency")]
        public int UpdatePeriodMinutes { get; set; }

        /// <summary>
        /// This defines the probability of sending decoys. This is configurable so we can tune the probability to server load if necessary.
        /// </summary>
        [JsonProperty("decoyProbability")]
        public int DecoyProbability { get; set; }
    }
}
