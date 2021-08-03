﻿using ColossalFramework;
using ColossalFramework.UI;
using ModsCommon;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static ColossalFramework.Math.VectorUtils;
using static ModsCommon.Utilities.VectorUtilsExtensions;

namespace NetworkMultitool
{
    public abstract class BaseCreateConnectionMode : BaseCreateMode
    {
        public List<Circle> Circles { get; private set; } = new List<Circle>();
        public List<Straight> Straights { get; private set; } = new List<Straight>();

        public int SelectCircle { get; protected set; }
        public bool SelectOffset { get; protected set; }

        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);

            if (prevMode is BaseCreateConnectionMode connectionMode)
            {
                First = connectionMode.First;
                Second = connectionMode.Second;
                IsFirstStart = connectionMode.IsFirstStart;
                IsSecondStart = connectionMode.IsSecondStart;

                FirstTrajectory = connectionMode.FirstTrajectory;
                SecondTrajectory = connectionMode.SecondTrajectory;

                SelectCircle = connectionMode.SelectCircle;
                SelectOffset = connectionMode.SelectOffset;

                Circles.AddRange(connectionMode.Circles);
                foreach (var circle in Circles)
                {
                    if (circle != null)
                        circle.Label = AddLabel();
                }

                Straights.AddRange(connectionMode.Straights);
                foreach (var straight in Straights)
                {
                    if (straight != null)
                        straight.Label = AddLabel();
                }
            }
        }
        protected override void ResetParams()
        {
            base.ResetParams();
            ResetData();
        }
        private void ResetData()
        {
            foreach (var circle in Circles)
            {
                if (circle?.Label != null)
                {
                    RemoveLabel(circle.Label);
                    circle.Label = null;
                }
            }
            foreach (var straight in Straights)
            {
                if (straight?.Label != null)
                {
                    RemoveLabel(straight.Label);
                    straight.Label = null;
                }
            }

            Circles.Clear();
            Straights.Clear();
        }
        protected override void ClearLabels()
        {
            base.ClearLabels();

            foreach (var circle in Circles)
            {
                if (circle != null)
                    circle.Label = null;
            }
            foreach (var straight in Straights)
            {
                if (straight != null)
                    straight.Label = null;
            }
        }
        public override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (State != Result.None)
            {
                foreach (var circle in Circles)
                    circle?.Update(State == Result.Calculated);

                var info = Info;
                foreach (var straight in Straights)
                    straight?.Update(info, State == Result.Calculated);
            }
        }

        protected override void Init(StraightTrajectory firstTrajectory, StraightTrajectory secondTrajectory)
        {
            ResetData();

            var first = new EdgeCircle(CircleType.First, AddLabel(), firstTrajectory);
            var last = new EdgeCircle(CircleType.Last, AddLabel(), secondTrajectory);
            Circles.Add(first);
            Circles.Add(last);
            Straights.Add(null);
            Straights.Add(null);
            Straights.Add(null);

            EdgeCircle.GetSides(first, last);
            Circle.SetConnect(first, last);
        }
        protected override IEnumerable<Point> Calculate()
        {
            var minRadius = MinPossibleRadius;
            var maxRadius = 1000f;
            foreach (var circle in Circles)
                circle.IsCorrect = circle.Calculate(minRadius, maxRadius, out var result);

            for (var i = 1; i < Circles.Count; i += 1)
            {
                var isCorrect = Circle.CheckRadii(Circles[i - 1], Circles[i]);
                Circles[i - 1].IsCorrect &= isCorrect;
                Circles[i].IsCorrect &= isCorrect;
            }

            for (var i = 0; i < Straights.Count; i += 1)
            {
                var label = Straights[i]?.Label ?? AddLabel();

                if (i == 0)
                    Straights[i] = (Circles.FirstOrDefault() as EdgeCircle).GetStraight(label);
                else if (i == Straights.Count - 1)
                    Straights[i] = (Circles.LastOrDefault() as EdgeCircle).GetStraight(label);
                else
                {
                    Circle.SetConnect(Circles[i - 1], Circles[i]);
                    Straights[i] = Circle.GetStraight(Circles[i - 1], Circles[i], label);
                }
            }

            State = Circles.All(c => c.IsCorrect) ? Result.Calculated : Result.WrongShape;
            return GetParts();
        }
        private IEnumerable<Point> GetParts()
        {
            var firstStr = Straights.First();
            if (firstStr.Length >= 8f)
            {
                foreach (var part in firstStr.Parts)
                    yield return part;
                yield return new Point(firstStr.EndPosition, firstStr.Direction);
            }

            for (var i = 0; i < Circles.Count + Straights.Count - 2; i += 1)
            {
                if (i % 2 == 0)
                {
                    var j = i / 2;
                    if (!Circles[j].IsCorrect)
                        continue;

                    foreach (var part in Circles[j].Parts)
                        yield return part;
                }
                else
                {
                    var j = i / 2 + 1;
                    if (!Circles[j - 1].IsCorrect && !Circles[j].IsCorrect)
                    {
                        yield return Point.Empty;
                        continue;
                    }

                    var straight = Straights[j];
                    if (straight.Length >= 8f)
                    {
                        yield return new Point(straight.StartPosition, straight.Direction);
                        foreach (var part in straight.Parts)
                            yield return part;
                        yield return new Point(straight.EndPosition, straight.Direction);
                    }
                    else
                        yield return new Point(straight.Position(0.5f), straight.Tangent(0.5f));
                }
            }

            var lastStr = Straights.Last();
            if (lastStr.Length >= 8f)
            {
                yield return new Point(lastStr.StartPosition, lastStr.Direction);
                foreach (var part in lastStr.Parts)
                    yield return part;
            }
        }

        protected override void RenderCalculatedOverlay(RenderManager.CameraInfo cameraInfo, NetInfo info)
        {
            foreach (var circle in Circles)
                circle.Render(cameraInfo, info, Colors.White, Underground);
            foreach (var straight in Straights)
                straight.Render(cameraInfo, info, Colors.White, Underground);
        }
        protected override void RenderFailedOverlay(RenderManager.CameraInfo cameraInfo, NetInfo info)
        {
            if (State == Result.BigRadius || State == Result.SmallRadius || State == Result.WrongShape)
            {
                foreach (var circle in Circles)
                {
                    circle.RenderCircle(cameraInfo, circle.IsCorrect ? Colors.Green : Colors.Red, Underground);
                    circle.RenderCenter(cameraInfo, circle.IsCorrect ? Colors.Green : Colors.Red, Underground);
                }
            }
        }

        protected class EdgeCircle : Circle
        {
            public CircleType Type { get; }
            private StraightTrajectory Guide { get; }

            public override Vector3 CenterPos
            {
                get => base.CenterPos;
                set
                {
                    var dir = Type switch
                    {
                        CircleType.First => StartRadiusDir,
                        CircleType.Last => EndRadiusDir,
                    };
                    var normal = new StraightTrajectory(value, value + dir, false);

                    Intersection.CalculateSingle(Guide, normal, out var t, out _);
                    Offset = Mathf.Clamp(t, 0f, 500f);
                }
            }
            public override Vector3 StartRadiusDir
            {
                get => base.StartRadiusDir;
                set
                {
                    if (Type == CircleType.Last)
                        base.StartRadiusDir = value;
                }
            }
            public override Vector3 EndRadiusDir
            {
                get => base.EndRadiusDir;
                set
                {
                    if (Type == CircleType.First)
                        base.EndRadiusDir = value;
                }
            }

            public override Vector3 StartPos => Type == CircleType.First ? Guide.Position(Offset) : base.StartPos;
            public override Vector3 EndPos => Type == CircleType.Last ? Guide.Position(Offset) : base.EndPos;
            public override Vector3 StartDir => Type == CircleType.First ? Guide.Direction : base.StartDir;
            public override Vector3 EndDir => Type == CircleType.Last ? -Guide.Direction : base.EndDir;

            private float _offset;
            public float Offset
            {
                get => _offset;
                set => _offset = Mathf.Max(value, 0f);
            }

            public EdgeCircle(CircleType type, InfoLabel label, StraightTrajectory guide) : base(label)
            {
                Type = type;
                Guide = guide;
            }

            public override bool Calculate(float minRadius, float maxRadius, out Result result)
            {
                if (!base.Calculate(minRadius, maxRadius, out result))
                    return false;

                var dir = Guide.Direction.Turn90(Direction == Direction.Right);
                if (Type == CircleType.First)
                    base.StartRadiusDir = -dir;
                else
                    base.EndRadiusDir = dir;

                base.CenterPos = Guide.StartPosition + (Type == CircleType.First ? dir : -dir) * Radius + Guide.Direction * Offset;

                result = Result.Calculated;
                return true;
            }
            public Straight GetStraight(InfoLabel label) => Type switch
            {
                CircleType.First => new Straight(Guide.StartPosition, StartPos, StartRadiusDir, label),
                CircleType.Last => new Straight(EndPos, Guide.StartPosition, EndRadiusDir, label),
            };

            public static void GetSides(EdgeCircle first, EdgeCircle last)
            {
                var connect = new StraightTrajectory(first.StartPos.MakeFlat(), last.EndPos.MakeFlat());

                var firstDir = CrossXZ(first.StartDir, connect.Direction) >= 0;
                var lastDir = CrossXZ(-last.EndDir, -connect.Direction) >= 0;
                var firstDot = DotXZ(first.StartDir, connect.Direction) >= 0;
                var lastDot = DotXZ(-last.EndDir, -connect.Direction) >= 0;

                if (firstDot != lastDot && firstDir != lastDir)
                {
                    if (!firstDot)
                        lastDir = !lastDir;
                    else if (!lastDot)
                        lastDir = !lastDir;
                }

                first.Direction = firstDir ? Direction.Right : Direction.Left;
                last.Direction = lastDir ? Direction.Left : Direction.Right;
            }
        }
        public enum CircleType
        {
            First,
            Last,
        }
    }
    public abstract class BaseAdditionalCreateConnectionMode : BaseCreateConnectionMode
    {
        public int Edit { get; protected set; }
        protected bool IsEdit => Edit != -1;

        public override void OnMouseUp(Event e)
        {
            Tool.SetMode(ToolModeType.CreateConnection);
        }

        protected override void RenderCalculatedOverlay(RenderManager.CameraInfo cameraInfo, NetInfo info)
        {
            for (var i = 0; i < Circles.Count; i += 1)
                Circles[i].RenderCircle(cameraInfo, i == Edit ? Colors.Green : Colors.Green.SetAlpha(64), Underground);

            base.RenderCalculatedOverlay(cameraInfo, info);
        }
        protected override void RenderFailedOverlay(RenderManager.CameraInfo cameraInfo, NetInfo info)
        {
            for (var i = 0; i < Circles.Count; i += 1)
                Circles[i].RenderCircle(cameraInfo, Colors.Red, Underground);

            base.RenderFailedOverlay(cameraInfo, info);
        }
    }
}
