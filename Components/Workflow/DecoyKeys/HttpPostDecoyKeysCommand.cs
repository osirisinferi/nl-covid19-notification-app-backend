﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Workflow.RegisterSecret;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Workflow.DecoyKeys
{
    public class HttpPostDecoyKeysCommand
    {
        private readonly ILogger _Logger;
        private readonly IRandomNumberGenerator _RandomNumberGenerator;
        private readonly IDecoyKeysConfig _Config;

        public HttpPostDecoyKeysCommand(ILogger<HttpPostDecoyKeysCommand> logger, IRandomNumberGenerator randomNumberGenerator, IDecoyKeysConfig config)
        {
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _RandomNumberGenerator = randomNumberGenerator ?? throw new ArgumentNullException(nameof(randomNumberGenerator));
            _Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<IActionResult> Execute()
        {
            var delayInMilliseconds = _RandomNumberGenerator.Next(_Config.MinimumDelayInMilliseconds, _Config.MaximumDelayInMilliseconds);

            _Logger.LogDebug("Delaying for {DelayInMilliseconds} seconds", delayInMilliseconds);
            await Task.Delay(delayInMilliseconds);

            return new OkResult();
        }
    }
}
