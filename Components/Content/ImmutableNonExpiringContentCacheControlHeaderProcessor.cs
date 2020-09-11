﻿using System;
using Microsoft.AspNetCore.Http;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Entities;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content
{
    public class ImmutableNonExpiringContentCacheControlHeaderProcessor
    {
        private const int Lifetime = 3600 * 24 * 90;

        public void Execute(HttpContext httpContext, ContentEntity _)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            httpContext.Response.Headers.Add("cache-control", $"public, immutable, max-age={ Lifetime}, s-maxage={ Lifetime }");
        }
    }
}