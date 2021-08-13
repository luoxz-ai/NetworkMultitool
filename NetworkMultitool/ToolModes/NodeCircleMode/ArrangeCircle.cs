﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ModsCommon;
using ModsCommon.Utilities;
using UnityEngine;
using static ColossalFramework.Math.VectorUtils;
using static ModsCommon.Utilities.VectorUtilsExtensions;

namespace NetworkMultitool
{
    public class ArrangeCircleMode : BaseNodeCircle
    {
        public override ToolModeType Type => ToolModeType.ArrangeAtCircle;
        private bool IsCompleteHover => Nodes.Count != 0 && ((HoverNode.Id == Nodes[0].Id && AddState == AddResult.InEnd) || (HoverNode.Id == Nodes[Nodes.Count - 1].Id && AddState == AddResult.InStart));

        protected override string GetInfo()
        {
            if (IsHoverNode && IsCompleteHover)
                return Localize.Mode_ArrangeCircle_Info_ClickToComplite + StepOverInfo;
            else
                return base.GetInfo();
        }
        protected override void Complite() => Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
        protected override void Apply() { }
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            RenderNearNodes(cameraInfo);
            RenderSegmentNodes(cameraInfo, AllowRenderNode);

            if (!IsHoverNode)
            {
                foreach (var node in Nodes)
                    node.Render(new OverlayData(cameraInfo) { Color = Colors.White, RenderLimit = Underground });
            }
            else if (IsCompleteHover)
            {
                foreach (var node in Nodes)
                    node.Render(new OverlayData(cameraInfo) { Color = Colors.Purple, RenderLimit = Underground });
                foreach (var node in ToAdd)
                    node.Render(new OverlayData(cameraInfo) { Color = Colors.Purple, RenderLimit = Underground });
            }
            else
            {
                RenderExistOverlay(cameraInfo);
                RenderAddedOverlay(cameraInfo);
            }
        }
    }
    public abstract class BaseArrangeCircleCompleteMode : BaseNetworkMultitoolMode
    {
        protected override bool IsReseted => true;
        public override bool CreateButton => false;
        protected override bool CanSwitchUnderground => false;

        protected List<CirclePoint> Nodes { get; } = new List<CirclePoint>();

        protected Vector3 Center { get; set; }
        protected float Radius { get; set; }

        private List<bool> States { get; } = new List<bool>();
        protected bool IsWrongOrder => States.Any(s => !s);
        protected bool IsBigDelta { get; private set; }
        protected Color CircleColor => IsWrongOrder ? Colors.Red : (IsBigDelta ? Colors.Orange : Colors.Green);

        private InfoLabel _label;
        public InfoLabel Label
        {
            get => _label;
            set
            {
                _label = value;
                if (_label != null)
                {
                    _label.textScale = 1.5f;
                    _label.opacity = 0.75f;
                    _label.isVisible = true;
                }
            }
        }

        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);
            Nodes.Clear();

            if (prevMode is ArrangeCircleMode arrangeMode)
                Calculate(arrangeMode.SelectedNodes, n => n.Id.GetNode().m_position, n => n.Id);
            else if (prevMode is BaseArrangeCircleCompleteMode arrangeCompliteMode)
            {
                Nodes.AddRange(arrangeCompliteMode.Nodes);
                Center = arrangeCompliteMode.Center;
                Radius = arrangeCompliteMode.Radius;
            }

            States.Clear();
            States.AddRange(Nodes.Select(_ => true));

            Label ??= AddLabel();
        }
        protected override void ClearLabels()
        {
            base.ClearLabels();
            Label = null;
        }
        public override void OnToolUpdate()
        {
            base.OnToolUpdate();

            var anglesList = new List<float>(Nodes.Count);
            var idxs = new List<int>(Nodes.Count);

            for (var i = 0; i < Nodes.Count; i += 1)
            {
                var index = anglesList.BinarySearch(Nodes[i].Angle);
                if (index < 0)
                    index = ~index;
                anglesList.Insert(index, Nodes[i].Angle);
                idxs.Insert(index, i);
            }

            IsBigDelta = false;
            for (var j = 0; j < idxs.Count; j += 1)
            {
                var i = (j - 1 + idxs.Count) % idxs.Count;
                var k = (j + 1) % idxs.Count;
                var delta1 = (idxs[j] != 0 ? idxs[j] : idxs.Count) - idxs[i];
                var delta2 = (idxs[k] != 0 ? idxs[k] : idxs.Count) - idxs[j];
                States[idxs[j]] = delta1 == 1 || delta2 == 1;
                IsBigDelta |= (anglesList[k] + (k == 0 ? Mathf.PI * 2f : 0f) - anglesList[j]) > Mathf.PI / 2f;
            }

            if(Label is InfoLabel label)
            {
                label.text = GetRadiusString(Radius);
                label.Direction = Tool.CameraDirection;
                label.WorldPosition = Center + Tool.CameraDirection * 5f;
            }
        }
        protected virtual void Calculate<Type>(IEnumerable<Type> source, Func<Type, Vector3> posGetter, Func<Type, ushort> idGetter)
        {
            var points = source.Select(s => posGetter(s)).ToArray();
            var centre = Vector3.zero;
            var radius = 1000f;

            for (var i = 0; i < points.Length; i += 1)
            {
                for (var j = i + 1; j < points.Length; j += 1)
                {
                    GetCircle2Points(points, i, j, ref centre, ref radius);

                    for (var k = j + 1; k < points.Length; k += 1)
                        GetCircle3Points(points, i, j, k, ref centre, ref radius);
                }
            }

            Center = centre;
            Radius = radius;

            foreach (var item in source)
                Nodes.Add(GetPoint(Center, idGetter(item), posGetter(item)));
        }
        protected CirclePoint GetPoint(Vector3 center, ushort id, Vector3 position)
        {
            position.y = center.y;
            var newPos = center + (position - center).MakeFlatNormalized() * Radius;
            var angle = (newPos - center).AbsoluteAngle();
            return new CirclePoint(id, angle);
        }
        private void GetCircle2Points(Vector3[] points, int i, int j, ref Vector3 centre, ref float radius)
        {
            var newCentre = (points[i] + points[j]) / 2;
            var newRadius = (points[i] - points[j]).magnitude / 2;

            if (newRadius >= radius)
                return;

            if (AllPointsInCircle(points, newCentre, newRadius, i, j))
            {
                centre = newCentre;
                radius = newRadius;
            }
        }
        private void GetCircle3Points(Vector3[] points, int i, int j, int k, ref Vector3 centre, ref float radius)
        {
            var pos1 = (points[i] + points[j]) / 2;
            var pos2 = (points[j] + points[k]) / 2;

            var dir1 = (points[i] - points[j]).Turn90(true).normalized;
            var dir2 = (points[j] - points[k]).Turn90(true).normalized;

            Line2.Intersect(XZ(pos1), XZ(pos1 + dir1), XZ(pos2), XZ(pos2 + dir2), out float p, out _);
            var newCentre = pos1 + dir1 * p;
            var newRadius = (newCentre - points[i]).magnitude;

            if (newRadius >= radius)
                return;

            if (AllPointsInCircle(points, newCentre, newRadius, i, j, k))
            {
                centre = newCentre;
                radius = newRadius;
            }
        }
        private bool AllPointsInCircle(Vector3[] points, Vector3 centre, float radius, params int[] ignore)
        {
            for (var i = 0; i < points.Length; i += 1)
            {
                if (ignore.Any(j => j == i))
                    continue;

                if ((centre - points[i]).magnitude > radius)
                    return false;
            }

            return true;
        }
        protected void RenderCircle(RenderManager.CameraInfo cameraInfo, Color color) => Center.RenderCircle(new OverlayData(cameraInfo) { Width = Radius * 2f, Color = color, RenderLimit = Underground });
        protected void RenderCenter(RenderManager.CameraInfo cameraInfo, Color color) => Center.RenderCircle(new OverlayData(cameraInfo) { Color = color, RenderLimit = Underground }, 5f, 0f);
        protected void RenderHoverCenter(RenderManager.CameraInfo cameraInfo, Color color) => Center.RenderCircle(new OverlayData(cameraInfo) { Color = color, RenderLimit = Underground }, 7f, 5f);
        protected void RenderNodes(RenderManager.CameraInfo cameraInfo, int hover = -1)
        {
            for (var i = 0; i < Nodes.Count; i += 1)
            {
                var color = States[i] ? Colors.White : Colors.Red;
                Nodes[i].GetPositions(Center, Radius, out var currentPos, out var newPos);
                if ((currentPos - newPos).sqrMagnitude > 1f)
                {
                    var line = new StraightTrajectory(currentPos, newPos);
                    line.Render(new OverlayData(cameraInfo) { Color = color, RenderLimit = Underground });
                }
                if (i == hover)
                    newPos.RenderCircle(new OverlayData(cameraInfo) { Color = States[i] ? Colors.Blue : Colors.White, RenderLimit = Underground }, 4f, 2f);
                newPos.RenderCircle(new OverlayData(cameraInfo) { Color = color, RenderLimit = Underground }, 2f, 0f);
            }
        }

        protected struct CirclePoint
        {
            public ushort Id;
            public float Angle;

            public CirclePoint(ushort id, float angle)
            {
                Id = id;
                Angle = angle % (Mathf.PI * 2f) + (angle < 0f ? Mathf.PI * 2f : 0f);
            }

            public void GetPositions(Vector3 center, float radius, out Vector3 currentPos, out Vector3 newPos)
            {
                currentPos = Id.GetNode().m_position;
                currentPos.y = center.y;
                newPos = center + Angle.Direction() * radius;
            }
            public CirclePoint Turn(float delta) => new CirclePoint(Id, Angle + delta);
        }
    }
    public class ArrangeCircleCompleteMode : BaseArrangeCircleCompleteMode
    {
        public static NetworkMultitoolShortcut ResetArrangeCircleShortcut { get; } = GetShortcut(KeyCode.Delete, nameof(ResetArrangeCircleShortcut), nameof(Localize.Settings_Shortcut_ResetArrangeCircle), () => (SingletonTool<NetworkMultitoolTool>.Instance.Mode as ArrangeCircleCompleteMode)?.Recalculate());

        public override ToolModeType Type => ToolModeType.ArrangeAtCircleComplete;

        private Vector3 DefaultCenter { get; set; }
        public bool IsHoverCenter { get; private set; }
        public bool IsHoverCircle { get; private set; }
        public int HoveredNode { get; private set; }
        public bool IsPressedCenter { get; private set; }
        public bool IsPressedCircle { get; private set; }
        public int PressedNode { get; private set; }

        public override IEnumerable<NetworkMultitoolShortcut> Shortcuts
        {
            get
            {
                yield return ApplyShortcut;
                yield return ResetArrangeCircleShortcut;
            }
        }

        protected override string GetInfo()
        {
            var result = string.Empty;
            if (IsWrongOrder)
                result += Localize.Mode_Info_ArrangeCircle_WrongOrder + "\n\n";
            else if (IsBigDelta)
                result += Localize.Mode_Info_ArrangeCircle_BigDelta + "\n\n";

            result +=
                string.Format(Localize.Mode_Info_ArrangeCircle_PressToReset, ResetArrangeCircleShortcut) + "\n" +
                string.Format(Localize.Mode_Info_ArrangeCircle_Apply, ApplyShortcut);

            return result;
        }
        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);

            IsHoverCenter = false;
            IsHoverCircle = false;
            HoveredNode = -1;
            IsPressedCenter = false;
            IsPressedCircle = false;
            PressedNode = -1;

            if (prevMode is ArrangeCircleCompleteMode mode)
            {
                Nodes.Clear();
                Calculate(mode.Nodes, n => n.Id.GetNode().m_position, n => n.Id);
            }
        }
        protected override void Calculate<Type>(IEnumerable<Type> source, Func<Type, Vector3> posGetter, Func<Type, ushort> idGetter)
        {
            base.Calculate(source, posGetter, idGetter);
            DefaultCenter = Center;
        }
        private void Recalculate()
        {
            var nodes = new List<CirclePoint>(Nodes);
            Nodes.Clear();
            Calculate(nodes, n => n.Id.GetNode().m_position, n => n.Id);
        }
        public override void OnToolUpdate()
        {
            base.OnToolUpdate();

            var mousePosition = XZ(GetMousePosition(Center.y));
            var magnitude = (XZ(Center) - mousePosition).magnitude;
            IsHoverCenter = Tool.MouseRayValid && magnitude <= 5f;
            IsHoverCircle = Tool.MouseRayValid && Radius - 5f <= magnitude && magnitude <= Radius + 5f;
            HoveredNode = -1;
            for (var i = 0; i < Nodes.Count; i += 1)
            {
                Nodes[i].GetPositions(Center, Radius, out _, out var position);
                if ((XZ(position) - mousePosition).sqrMagnitude <= 4f)
                {
                    HoveredNode = i;
                    break;
                }
            }
        }
        public override void OnMouseDown(Event e)
        {
            IsPressedCenter = IsHoverCenter;
            IsPressedCircle = IsHoverCircle;
            PressedNode = HoveredNode;
        }
        public override void OnMouseDrag(Event e)
        {
            if (IsPressedCenter)
                Tool.SetMode(ToolModeType.ArrangeAtCircleMoveCenter);
            else if (PressedNode != -1)
                Tool.SetMode(ToolModeType.ArrangeAtCircleMoveNode);
            else if (IsPressedCircle)
                Tool.SetMode(ToolModeType.ArrangeAtCircleRadius);
        }
        public override void OnPrimaryMouseDoubleClicked(Event e)
        {
            if (IsHoverCenter)
                Center = DefaultCenter;
            else if (HoveredNode != -1)
                Nodes[HoveredNode] = GetPoint(DefaultCenter, Nodes[HoveredNode].Id, Nodes[HoveredNode].Id.GetNode().m_position);
        }
        public override void OnSecondaryMouseClicked() => Tool.SetMode(ToolModeType.ArrangeAtCircle);
        public override bool OnEscape()
        {
            Tool.SetMode(ToolModeType.ArrangeAtCircle);
            return true;
        }
        protected override void Apply()
        {
            if (IsWrongOrder)
                return;

            var segmentIds = new ushort[Nodes.Count];
            for (var i = 0; i < Nodes.Count; i += 1)
                NetExtension.GetCommon(Nodes[i].Id, Nodes[(i + 1) % Nodes.Count].Id, out segmentIds[i]);
            var terrainRect = GetTerrainRect(segmentIds);

            foreach (var node in Nodes)
            {
                node.GetPositions(Center, Radius, out _, out var newPos);
                MoveNode(node.Id, newPos);
            }
            for (var i = 0; i < Nodes.Count; i += 1)
            {
                SetDirection(i, (i + 1) % Nodes.Count);
                SetDirection(i, (i + Nodes.Count - 1) % Nodes.Count);
            }

            foreach (var node in Nodes)
                NetManager.instance.UpdateNode(node.Id);

            UpdateTerrain(terrainRect);

            Tool.SetMode(ToolModeType.ArrangeAtCircle);
        }
        private void SetDirection(int i, int j)
        {
            var centerDir = (Nodes[i].Id.GetNode().m_position - Center).MakeFlatNormalized();

            NetExtension.GetCommon(Nodes[i].Id, Nodes[j].Id, out var segmentId);
            ref var segment = ref segmentId.GetSegment();
            var direction = Nodes[j].Id.GetNode().m_position - Nodes[i].Id.GetNode().m_position;
            var newDirection = centerDir.Turn90(NormalizeCrossXZ(centerDir, direction) >= 0f);
            SetSegmentDirection(segmentId, segment.IsStartNode(Nodes[i].Id), newDirection);
        }
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var color = CircleColor;
            RenderCircle(cameraInfo, IsHoverCircle && !IsHoverCenter && HoveredNode == -1 ? Colors.Blue : color);
            RenderNodes(cameraInfo, IsHoverCenter ? -1 : HoveredNode);
            if (IsHoverCenter)
                RenderHoverCenter(cameraInfo, Colors.White);
            RenderCenter(cameraInfo, color);
        }
    }
    public class ArrangeCircleMoveCenterMode : BaseArrangeCircleCompleteMode
    {
        public override ToolModeType Type => ToolModeType.ArrangeAtCircleMoveCenter;
        private Vector3 PrevPos { get; set; }

        protected override string GetInfo() => Localize.Mode_Connection_Info_SlowMove;
        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);
            PrevPos = GetMousePosition(Center.y);
        }
        public override void OnMouseUp(Event e) => Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
        public override bool OnEscape()
        {
            Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
            return true;
        }
        public override void OnMouseDrag(Event e)
        {
            var newPos = GetMousePosition(Center.y);
            var dir = newPos - PrevPos;
            PrevPos = newPos;

            if (Utility.OnlyCtrlIsPressed)
                Center += dir * 0.1f;
            else if (Utility.OnlyAltIsPressed)
                Center += dir * 0.01f;
            else
                Center += dir;
        }
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var color = CircleColor;
            RenderCircle(cameraInfo, color);
            RenderNodes(cameraInfo);
            RenderHoverCenter(cameraInfo, Colors.White);
            RenderCenter(cameraInfo, color);
        }
    }
    public class ArrangeCircleRadiusMode : BaseArrangeCircleCompleteMode
    {
        public override ToolModeType Type => ToolModeType.ArrangeAtCircleRadius;
        private float MinRadius { get; set; }

        protected override string GetInfo() => Localize.Mode_Connection_Info_RadiusStep;
        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);

            var minRadius = 0f;
            for (var i = 0; i < Nodes.Count; i += 1)
            {
                NetExtension.GetCommon(Nodes[i].Id, Nodes[(i + 1) % Nodes.Count].Id, out var segmentId);
                minRadius = Mathf.Max(minRadius, segmentId.GetSegment().Info.m_halfWidth * 2f);
            }
            MinRadius = minRadius;
        }
        public override void OnMouseUp(Event e) => Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
        public override bool OnEscape()
        {
            Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
            return true;
        }
        public override void OnMouseDrag(Event e)
        {
            var radius = (XZ(GetMousePosition(Center.y)) - XZ(Center)).magnitude;

            if (Utility.OnlyShiftIsPressed)
                radius = radius.RoundToNearest(10f);
            else if (Utility.OnlyCtrlIsPressed)
                radius = radius.RoundToNearest(1f);
            else if (Utility.OnlyAltIsPressed)
                radius = radius.RoundToNearest(0.1f);

            Radius = Mathf.Max(radius, MinRadius);
        }
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var color = CircleColor;
            RenderCircle(cameraInfo, color);
            RenderNodes(cameraInfo);
            RenderCenter(cameraInfo, color);
        }
    }
    public class ArrangeCircleMoveNodeMode : BaseArrangeCircleCompleteMode
    {
        public override ToolModeType Type => ToolModeType.ArrangeAtCircleMoveNode;
        private int Edit { get; set; }
        private Vector3 PrevDir { get; set; }

        protected override string GetInfo()
        {
            var result = string.Empty;
            if (IsWrongOrder)
                result += Localize.Mode_Info_ArrangeCircle_WrongOrder + "\n\n";
            result += Localize.Mode_Info_ArrangeCircle_MoveAll;
            return result;
        }
        protected override void Reset(IToolMode prevMode)
        {
            base.Reset(prevMode);
            Edit = prevMode is ArrangeCircleCompleteMode mode ? mode.PressedNode : -1;
            PrevDir = GetMousePosition(Center.y) - Center;
        }
        public override void OnMouseUp(Event e) => Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
        public override bool OnEscape()
        {
            Tool.SetMode(ToolModeType.ArrangeAtCircleComplete);
            return true;
        }
        public override void OnMouseDrag(Event e)
        {
            var direction = GetMousePosition(Center.y) - Center;
            var delta = MathExtention.GetAngle(PrevDir, direction);
            PrevDir = direction;

            if (Utility.OnlyShiftIsPressed)
            {
                for (var i = 0; i < Nodes.Count; i += 1)
                    Nodes[i] = Nodes[i].Turn(delta);
            }
            else if (Edit != -1)
                Nodes[Edit] = Nodes[Edit].Turn(delta);
        }
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var color = CircleColor;
            RenderCircle(cameraInfo, color);
            RenderNodes(cameraInfo, Utility.OnlyShiftIsPressed ? -1 : Edit);
            RenderCenter(cameraInfo, color);
        }
    }
}
