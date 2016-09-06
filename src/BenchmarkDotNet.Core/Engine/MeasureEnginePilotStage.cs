﻿using System;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Reports;

namespace BenchmarkDotNet.Engine
{
    internal class MeasureEnginePilotStage : MeasureEngineStage
    {
        internal const long MaxInvokeCount = (long.MaxValue / 2 + 1) / 2;

        public MeasureEnginePilotStage(IMeasureEngine engine) : base(engine)
        {
        }

        /// <returns>Perfect invocation count</returns>
        public long Run()
        {
            // Here we want to guess "perfect" amount of invocation
            return TargetJob.IterationTime.IsAuto ? RunAuto() : RunSpecific();
        }

        /// <summary>
        /// A case where we don't have specific iteration time.
        /// </summary>
        private long RunAuto()
        {
            long invokeCount = TargetAccuracy.MinInvokeCount;
            double maxError = TargetAccuracy.MaxStdErrRelative; // TODO: introduce a StdErr factor
            double minIterationTome = TimeUnit.Convert(MeasureEngine.MinIterationTimeMs, TimeUnit.Millisecond, TimeUnit.Nanosecond);

            double resolution = TargetClock.GetResolution().Nanoseconds;

            int iterationCounter = 0;
            while (true)
            {
                iterationCounter++;
                var measurement = RunIteration(IterationMode.Pilot, iterationCounter, invokeCount);
                double iterationTime = measurement.Nanoseconds;                
                double operationError = 2.0 * resolution / invokeCount; // An operation error which has arisen due to the Chronometer precision
                double operationMaxError = iterationTime / invokeCount * maxError; // Max acceptable operation error

                bool isFinished = operationError < operationMaxError && iterationTime >= minIterationTome;
                if (isFinished)
                    break;
                if (invokeCount >= MaxInvokeCount)
                    break;

                invokeCount *= 2;
            }
            WriteLine();

            return invokeCount;
        }

        /// <summary>
        /// A case where we have specific iteration time.
        /// </summary>
        private long RunSpecific()
        {
            long invokeCount = MeasureEngine.MinInvokeCount;
            double targetIterationTime = TimeUnit.Convert(TargetJob.IterationTime, TimeUnit.Millisecond, TimeUnit.Nanosecond);
            int iterationCounter = 0;

            int downCount = 0; // Amount of iterations where newInvokeCount < invokeCount
            while (true)
            {
                iterationCounter++;
                var measurement = RunIteration(IterationMode.Pilot, iterationCounter, invokeCount);
                double actualIterationTime = measurement.Nanoseconds;
                long newInvokeCount = Math.Max(TargetAccuracy.MinInvokeCount, (long) Math.Round(invokeCount * targetIterationTime / actualIterationTime));

                if (newInvokeCount < invokeCount)
                    downCount++;

                if (Math.Abs(newInvokeCount - invokeCount) <= 1 || downCount >= 3)
                    break;

                invokeCount = newInvokeCount;
            }
            WriteLine();

            return invokeCount;
        }
    }
}