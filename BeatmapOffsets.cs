using System.Collections.Generic;

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace JDFixer
{
    internal static class BeatmapOffsets
    {
        private const int MaxSnapPointsPerDirection = 2048;
        internal static List<float> JD_Snap_Points = new List<float>();
        internal static List<float> RT_Snap_Points = new List<float>();

        internal static List<string> JD_Offset_Points = new List<string>();
        internal static List<string> RT_Offset_Points = new List<string>();

        internal static float jd_snap_value = 0f;
        internal static float rt_snap_value = 0f;

        internal static string jd_offset_snap_value = "";
        internal static string rt_offset_snap_value = "";

        private static readonly object PreparationGate = new object();
        private static Task<SnapResult> preparationTask;
        private static int preparationGeneration;

        internal sealed class SnapResult
        {
            internal readonly List<float> JDPoints = new List<float>();
            internal readonly List<string> JDOffsets = new List<string>();
            internal readonly List<float> RTPoints = new List<float>();
            internal readonly List<string> RTOffsets = new List<string>();
        }

        private sealed class SnapRequest
        {
            private readonly float offset, jd, jdStep, jdMin, jdMax, rt, rtStep, rtMin, rtMax, fraction;
            private readonly CultureInfo culture;

            internal SnapRequest(BeatmapInfo map, float fraction, CultureInfo culture)
            {
                offset = map.Offset;
                jd = map.JumpDistance;
                jdStep = map.JDOffsetQuantum;
                jdMin = map.MinJDSlider;
                jdMax = map.MaxJDSlider;
                rt = map.ReactionTime;
                rtStep = map.RTOffsetQuantum;
                rtMin = map.MinRTSlider;
                rtMax = map.MaxRTSlider;
                this.fraction = fraction;
                this.culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
            }

            internal SnapResult Build()
            {
                var result = new SnapResult();
                BuildPoints(result.JDPoints, result.JDOffsets, jd, jdStep, jdMin, jdMax);
                BuildPoints(result.RTPoints, result.RTOffsets, rt, rtStep, rtMin, rtMax);
                return result;
            }

            private void BuildPoints(List<float> points, List<string> offsets, float value, float step, float minimum, float maximum)
            {
                points.Add(value);
                offsets.Add("( 0, " + offset.ToString("0.##", culture) + " )");
                if (step <= 0f || float.IsNaN(step) || float.IsInfinity(step)) return;

                for (int multiple = 1; multiple <= MaxSnapPointsPerDirection; multiple++)
                {
                    float point = value + multiple * step;
                    if (point > maximum || point == points[points.Count - 1]) break;
                    points.Add(point);
                    offsets.Add(FormatOffset(multiple));
                }

                var lowerPoints = new List<float>();
                var lowerOffsets = new List<string>();
                for (int multiple = -1; multiple >= -MaxSnapPointsPerDirection; multiple--)
                {
                    float point = value + multiple * step;
                    if (point < minimum || (lowerPoints.Count == 0 ? point == value : point == lowerPoints[lowerPoints.Count - 1])) break;
                    lowerPoints.Add(point);
                    lowerOffsets.Add(FormatOffset(multiple));
                }
                lowerPoints.Reverse();
                lowerOffsets.Reverse();
                points.InsertRange(0, lowerPoints);
                offsets.InsertRange(0, lowerOffsets);
            }

            private string FormatOffset(int multiple)
            {
                return "( " + multiple.ToString(culture) + "/" + fraction.ToString(culture) + ", " +
                    (offset + multiple / fraction).ToString("0.##", culture) + " )";
            }
        }

        internal sealed class SnapPreparation
        {
            private readonly BeatmapInfo map;
            private readonly CultureInfo culture;
            private readonly float fraction;
            private readonly int generation;
            private readonly Task<SnapResult> task;

            internal SnapPreparation(BeatmapInfo map, CultureInfo culture, float fraction, int generation, Task<SnapResult> task)
            {
                this.map = map;
                this.culture = culture;
                this.fraction = fraction;
                this.generation = generation;
                this.task = task;
            }

            internal bool TryApply(BeatmapInfo current)
            {
                if (!ReferenceEquals(map, current) || generation != preparationGeneration ||
                    !ReferenceEquals(culture, CultureInfo.CurrentCulture) || fraction != PluginConfig.Instance.offset_fraction)
                    return false;

                SnapResult result;
                try { result = task.GetAwaiter().GetResult(); }
                catch { return false; }
                if (result == null) return false;

                JD_Snap_Points.Clear();
                JD_Snap_Points.AddRange(result.JDPoints);
                JD_Offset_Points.Clear();
                JD_Offset_Points.AddRange(result.JDOffsets);
                RT_Snap_Points.Clear();
                RT_Snap_Points.AddRange(result.RTPoints);
                RT_Offset_Points.Clear();
                RT_Offset_Points.AddRange(result.RTOffsets);
                return true;
            }
        }

        internal static SnapPreparation Prepare_Snap_Points(BeatmapInfo map)
        {
            var culture = CultureInfo.CurrentCulture;
            if (culture.GetType() != typeof(CultureInfo) || !culture.IsReadOnly || !culture.NumberFormat.IsReadOnly)
                return null;
            if (EstimatedPoints(map.JumpDistance, map.JDOffsetQuantum, map.MinJDSlider, map.MaxJDSlider) +
                EstimatedPoints(map.ReactionTime, map.RTOffsetQuantum, map.MinRTSlider, map.MaxRTSlider) < 256f)
                return null;

            lock (PreparationGate)
            {
                if (preparationTask != null) return null;
                var fraction = PluginConfig.Instance.offset_fraction;
                var request = new SnapRequest(map, fraction, culture);
                var task = Task.Factory.StartNew(request.Build, CancellationToken.None,
                    TaskCreationOptions.None, TaskScheduler.Default);
                preparationTask = task;
                task.ContinueWith(ReleasePreparation, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var result = task.ContinueWith(GetPreparedResult, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return new SnapPreparation(map, culture, fraction, preparationGeneration, result);
            }
        }

        private static float EstimatedPoints(float value, float step, float minimum, float maximum)
        {
            if (step <= 0f || float.IsNaN(step) || float.IsInfinity(step)) return 0f;
            return Math.Min(2 * MaxSnapPointsPerDirection, Math.Max(0f, (maximum - value) / step) + Math.Max(0f, (value - minimum) / step));
        }

        private static void ReleasePreparation(Task<SnapResult> completed)
        {
            _ = completed.Exception;
            lock (PreparationGate)
                if (ReferenceEquals(preparationTask, completed)) preparationTask = null;
        }

        private static SnapResult GetPreparedResult(Task<SnapResult> completed)
        {
            try { return completed.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        internal static void Retire_Snap_Preparation()
        {
            preparationGeneration++;
        }


        internal static void Create_Snap_Points(ref List<float> Snap_Points, ref List<string> Offset_Points, float _selectedBeatmap_Offset, float _selectedBeatmap_JD_RT, float _selectedBeatmap_UnitOffset, float _selectedBeatmap_MinSlider, float _selectedBeatmap_MaxSlider)
        {
            //Plugin.Log.Debug("Create Snap Points");
            //Plugin.Log.Debug("Min: " + _selectedBeatmap_MinSlider + " " + _selectedBeatmap_MaxSlider);

            Snap_Points.Clear();
            Snap_Points.Add(_selectedBeatmap_JD_RT);

            Offset_Points.Clear();
            Offset_Points.Add("( 0, " + _selectedBeatmap_Offset.ToString("0.##") + " )");

            if (_selectedBeatmap_UnitOffset <= 0f || float.IsNaN(_selectedBeatmap_UnitOffset) || float.IsInfinity(_selectedBeatmap_UnitOffset))
                return;

            for (int multiple = 1; multiple <= MaxSnapPointsPerDirection; multiple++)
            {
                float point = _selectedBeatmap_JD_RT + multiple * _selectedBeatmap_UnitOffset;
                if (point > _selectedBeatmap_MaxSlider || point == Snap_Points[Snap_Points.Count - 1]) break;
                Snap_Points.Add(point);
                Offset_Points.Add("( " + multiple + "/" + PluginConfig.Instance.offset_fraction + ", " + (_selectedBeatmap_Offset + multiple / PluginConfig.Instance.offset_fraction).ToString("0.##") + " )");
            }

            var lowerPoints = new List<float>();
            var lowerOffsets = new List<string>();
            for (int multiple = -1; multiple >= -MaxSnapPointsPerDirection; multiple--)
            {
                float point = _selectedBeatmap_JD_RT + multiple * _selectedBeatmap_UnitOffset;
                if (point < _selectedBeatmap_MinSlider || (lowerPoints.Count == 0 ? point == _selectedBeatmap_JD_RT : point == lowerPoints[lowerPoints.Count - 1])) break;
                lowerPoints.Add(point);
                lowerOffsets.Add("( " + multiple + "/" + PluginConfig.Instance.offset_fraction + ", " + (_selectedBeatmap_Offset + multiple / PluginConfig.Instance.offset_fraction).ToString("0.##") + " )");
            }
            lowerPoints.Reverse();
            lowerOffsets.Reverse();
            Snap_Points.InsertRange(0, lowerPoints);
            Offset_Points.InsertRange(0, lowerOffsets);

            // Debug:
            /*for (int i = 0; i < Snap_Points.Count; i++)
            {
                Plugin.Log.Debug(i + ": " + Snap_Points[i]);
                Plugin.Log.Debug(i + ": " + Offset_Points[i]);
            }*/
        }


        internal static void Calculate_Nearest_JD_Snap_Point(float JD_Value)
        {
            //Plugin.Log.Debug("Count: " + JD_Snap_Points.Count + " " + JD_Value);

            if (JD_Snap_Points.Count == 0)
            {
                //Plugin.Log.Debug("empty: " + JD_Value);

                jd_offset_snap_value = "";
                jd_snap_value = JD_Value;

                return;
            }

            for (int i = 0; i < JD_Snap_Points.Count; i++)
            {
                //Plugin.Log.Debug(i + ": " + JD_Snap_Points[i]);

                if (JD_Snap_Points[i] >= JD_Value)
                {
                    jd_offset_snap_value = JD_Offset_Points[i];
                    jd_snap_value = JD_Snap_Points[i];

                    return;
                }
            }

            jd_offset_snap_value = JD_Offset_Points[JD_Offset_Points.Count - 1];
            jd_snap_value = JD_Snap_Points[JD_Snap_Points.Count - 1];
        }


        internal static void Calculate_Nearest_RT_Snap_Point(float RT_Value)
        {
            //Plugin.Log.Debug("Count: " + RT_Snap_Points.Count + " " + RT_Value);

            if (RT_Snap_Points.Count == 0)
            {
                //Plugin.Log.Debug("empty: " + RT_Value);

                rt_offset_snap_value = "";
                rt_snap_value = RT_Value;

                return;
            }

            for (int i = 0; i < RT_Snap_Points.Count; i++)
            {
                //Plugin.Log.Debug(i + ": " + RT_Snap_Points[i]);

                if (RT_Snap_Points[i] >= RT_Value)
                {
                    rt_offset_snap_value = RT_Offset_Points[i];
                    rt_snap_value = RT_Snap_Points[i];

                    return;
                }
            }

            rt_offset_snap_value = RT_Offset_Points[RT_Offset_Points.Count - 1];
            rt_snap_value = RT_Snap_Points[RT_Snap_Points.Count - 1];
        }
    }
}
