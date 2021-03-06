// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System.ComponentModel.DataAnnotations.Schema;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Entities
{
    [Table(TableNames.DiagnosisKeys)]
    public class DiagnosisKeys
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
    }
}