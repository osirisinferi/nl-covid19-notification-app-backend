﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content
{
    public class ZippedContentBuilder
    {
        public async Task<byte[]> Build(ZippedContentBuilderArgs[] args)
        {
            await using var result = new MemoryStream();
            using (var archive = new ZipArchive(result, ZipArchiveMode.Create, true))
            {
                foreach(var i in args)
                    await archive.WriteEntry(i.EntryName, i.Value);
            }

            return result.ToArray();
        }
    }
}