using System.Collections.Generic;

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
