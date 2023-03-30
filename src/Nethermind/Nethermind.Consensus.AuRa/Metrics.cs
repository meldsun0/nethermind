// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;
using Nethermind.Core.Attributes;

namespace Nethermind.Consensus.AuRa
{
    public static class Metrics
    {
        [GaugeMetric]
        [Description("Current AuRa step")]
        public static long AuRaStep { get; set; }

        [CounterMetric]
        [Description("Number of reported benign misbehaviour validators")]
        public static long ReportedBenignMisbehaviour { get; set; }

        [CounterMetric]
        [Description("Number of reported malicious misbehaviour validators")]
        public static long ReportedMaliciousMisbehaviour { get; set; }

        [GaugeMetric]
        [Description("Number of current AuRa validators")]
        public static long ValidatorsCount { get; set; }

        [CounterMetric]
        [Description("Number of sealed transactions generated by engine")]
        public static long SealedTransactions { get; set; }

        [CounterMetric]
        [Description("RANDAO number of commit hash transactions")]
        public static long CommitHashTransaction { get; set; }

        [CounterMetric]
        [Description("RANDAO number of reveal number transactions")]
        public static long RevealNumber { get; set; }

        [CounterMetric]
        [Description("POSDAO number of emit init change transactions")]
        public static long EmitInitiateChange { get; set; }
    }
}
