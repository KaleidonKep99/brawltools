﻿using System;
using BrawlLib.OpenGL;
using System.ComponentModel;
using BrawlLib.SSBB.ResourceNodes;
using System.IO;
using BrawlLib.Modeling;
using BrawlLib.Wii.Animations;
using System.Collections.Generic;
using BrawlLib.SSBBTypes;
using BrawlLib.IO;
using BrawlLib;
using System.Drawing.Imaging;
using Gif.Components;
using OpenTK.Graphics.OpenGL;
using BrawlLib.Imaging;
using System.Drawing;

namespace System.Windows.Forms
{
    public partial class ModelEditControl : UserControl, IMainWindow
    {
        #region Mouse Down

        public bool Ctrl { get { return (ModifierKeys & Keys.Control) == Keys.Control; } }
        public bool Alt { get { return (ModifierKeys & Keys.Alt) == Keys.Alt; } }
        public bool Shift { get { return (ModifierKeys & Keys.Shift) == Keys.Shift; } }
        public bool CtrlAlt { get { return Ctrl && Alt; } }
        public bool NotCtrlAlt { get { return !Ctrl && !Alt; } }

        private void modelPanel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                _temp = _selectedVertices;
                if (dontHighlightBonesAndVerticesToolStripMenuItem.Checked)
                {
                    HighlightStuff(e);
                    ModelPanel.Cursor = Cursors.Default;
                }

                //Reset snap flags
                _snapX = _snapY = _snapZ = _snapCirc = false;

                //Re-target selected bone
                IBoneNode bone = SelectedBone;
                if (bone != null)
                {
                    _snapX = _hiX;
                    _snapY = _hiY;
                    _snapZ = _hiZ;
                    _snapCirc = _hiCirc;

                    //Targeting functions are done in HighlightStuff
                    if (!(_snapX || _snapY || _snapZ || _snapCirc || _hiSphere))
                    {
                        //Orb selection missed. Assign bone and move to next step.
                        SelectedBone = bone = null;
                        goto GetBone;
                    }

                    //Bone re-targeted. Get frame values and local point aligned to snapping plane.

                    Vector3 point;
                    if (GetOrbPoint(new Vector2(e.X, e.Y), out point))
                    {
                        _firstPointBone = _lastPointBone = bone.InverseMatrix * (_lastPointWorld = _firstPointWorld = point);
                        if (_editType == TransformType.Rotation)
                        {
                            _rotating = true;
                            _oldAngles = bone.FrameState._rotate;
                        }
                        else if (_editType == TransformType.Translation)
                        {
                            _translating = true;
                            _oldPosition = bone.FrameState._translate;
                        }
                        else if (_editType == TransformType.Scale)
                        {
                            _scaling = true;
                            _oldScale = bone.FrameState._scale;
                        }
                        ModelPanel.AllowSelection = false;
                        if (_rotating || _translating || _scaling)
                            BoneChange(SelectedBone);
                    }
                }

            GetBone:

                //Try selecting new bone
                if (bone == null && RenderBones)
                    SelectedBone = _hiBone;

            bool ok = false;

                //Re-target selected vertex
                if (VertexLoc != null)
                {
                    _snapX = _hiX;
                    _snapY = _hiY;
                    _snapZ = _hiZ;
                    _snapCirc = _hiCirc;

                    if (!(_snapX || _snapY || _snapZ || _snapCirc || _hiSphere))
                    {
                        if (NotCtrlAlt)
                            ResetVertexColors();

                        ok = true;
                        goto GetVertex;
                    }

                    //Vertex re-targeted. Get translation and point (aligned to snapping plane).

                    Vector3 point;
                    if (GetVertexOrbPoint(new Vector2(e.X, e.Y), ((Vector3)VertexLoc), out point))
                    {
                        ModelPanel.AllowSelection = false;
                        if (_editType == TransformType.Rotation)
                        {
                            _rotating = true;
                            _oldAngles = new Vector3();
                        }
                        else if (_editType == TransformType.Translation)
                        {
                            _translating = true;
                            _oldPosition = ((Vector3)VertexLoc);
                        }
                        _lastPointWorld = point;
                        VertexChange(_selectedVertices);
                    }
                }
                else 
                    ok = true;

            GetVertex:

                if (ok)
                {
                    if (_hiVertex != null)
                    {
                        if (Ctrl)
                            if (!_selectedVertices.Contains(_hiVertex))
                            {
                                _selectedVertices.Add(_hiVertex);
                                _hiVertex._selected = true;
                                _hiVertex._highlightColor = Color.Orange;
                            }
                            else
                            {
                                _selectedVertices.Remove(_hiVertex);
                                _hiVertex._selected = false;
                                _hiVertex._highlightColor = Color.Transparent;
                            }
                        else if (Alt)
                            if (_selectedVertices.Contains(_hiVertex))
                            {
                                _selectedVertices.Remove(_hiVertex);
                                _hiVertex._selected = false;
                                _hiVertex._highlightColor = Color.Transparent;
                            }
                            else { }
                        else
                        {
                            ResetVertexColors();
                            if (_hiVertex != null)
                            {
                                _selectedVertices.Add(_hiVertex);
                                _hiVertex._selected = true;
                                _hiVertex._highlightColor = Color.Orange;
                            }
                        }
                    }
                    else if (NotCtrlAlt)
                        ResetVertexColors();
                }

                //Ensure a redraw so the snapping indicators are correct
                ModelPanel.Invalidate();
            }
        }

        public bool IsInTriangle(Vector3 point, Vector3 triPt1, Vector3 triPt2, Vector3 triPt3)
        {
            Vector3 v0 = triPt2 - triPt1;
            Vector3 v1 = triPt3 - triPt1;
            Vector3 v2 = point - triPt1;

            float dot00 = v0.Dot(v0);
            float dot01 = v0.Dot(v1);
            float dot02 = v0.Dot(v2);
            float dot11 = v1.Dot(v1);
            float dot12 = v1.Dot(v2);

            //Get barycentric coordinates
            float d = (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) / d;
            float v = (dot00 * dot12 - dot01 * dot02) / d;

            return u >= 0 && v >= 0 && u + v < 1;
        }

        #endregion

        #region Mouse Up

        private void modelPanel1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                //if (_rotating) btnUndo.Enabled = true;

                if (_rotating || _translating || _scaling)
                    if (VertexLoc == null) {
                        BoneChange(SelectedBone);
                        if (chkSnapToColl.Checked) SnapYIfClose();
                    } else {
                        VertexChange(_selectedVertices);
                    }

                _snapX = _snapY = _snapZ = _snapCirc = false;
                _rotating = _translating = _scaling = false;
                ModelPanel.AllowSelection = true;
                //if (modelPanel1._selecting)
                //{
                if (weightEditor.TargetVertices != _selectedVertices)
                    weightEditor.TargetVertices = _selectedVertices;
                if (vertexEditor.TargetVertices != _selectedVertices)
                    vertexEditor.TargetVertices = _selectedVertices;
                    //ModelPanel.Selecting = false;
                //}
            }
        }

        #endregion

        #region Mouse Move

        private unsafe void modelPanel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (_playing)
                return;

            bool moving = _scaling || _rotating || _translating;

            IBoneNode bone = SelectedBone;

            if (moving)
            {
                Vector3 point;
                if (bone != null)
                {
                    if (GetOrbPoint(new Vector2(e.X, e.Y), out point))
                    {
                        //Convert to local point
                        Vector3 lPoint = bone.InverseMatrix * point;

                        //Check for change in selection.
                        if (_lastPointBone != lPoint)
                        {
                            switch (_editType)
                            {
                                case TransformType.Scale:
                                    if (!_snapX) point._x = _lastPointWorld._x;
                                    if (!_snapY) point._y = _lastPointWorld._y;
                                    if (!_snapZ) point._z = _lastPointWorld._z;

                                    if (_snapX && _snapY && _snapZ)
                                    {
                                        //Get scale factor
                                        float scale = (point / _lastPointWorld)._y;

                                        if (scale != 0)
                                        {
                                            ApplyScale(0, scale);
                                            ApplyScale(1, scale);
                                            ApplyScale(2, scale);
                                        }
                                    }
                                    else
                                    {
                                        lPoint = bone.InverseMatrix * point;

                                        Vector3 point1 = bone.FrameState._transform * lPoint;
                                        Vector3 point2 = bone.FrameState._transform * _lastPointBone;

                                        Vector3 scale = (point1 / point2);

                                        if (scale._x != 0.0f) ApplyScale(0, scale._x);
                                        if (scale._y != 0.0f) ApplyScale(1, scale._y);
                                        if (scale._z != 0.0f) ApplyScale(2, scale._z);
                                    }
                                    break;

                                case TransformType.Rotation:

                                    //Get matrix with new rotation applied
                                    Matrix m = bone.FrameState._transform * Matrix.AxisAngleMatrix(_lastPointBone, lPoint);

                                    //Derive angles from matrices, get difference
                                    Vector3 angles = m.GetAngles() - bone.FrameState._transform.GetAngles();

                                    //Truncate (allows winding)
                                    if (angles._x > 180.0f) angles._x -= 360.0f;
                                    if (angles._y > 180.0f) angles._y -= 360.0f;
                                    if (angles._z > 180.0f) angles._z -= 360.0f;
                                    if (angles._x < -180.0f) angles._x += 360.0f;
                                    if (angles._y < -180.0f) angles._y += 360.0f;
                                    if (angles._z < -180.0f) angles._z += 360.0f;

                                    //Apply difference to axes that have changed (pnlAnim should handle this so keyframes are created)
                                    if (angles._x != 0.0f) ApplyAngle(0, angles._x);
                                    if (angles._y != 0.0f) ApplyAngle(1, angles._y);
                                    if (angles._z != 0.0f) ApplyAngle(2, angles._z);
                                    break;

                                case TransformType.Translation:
                                    
                                    if (!_snapX) point._x = _lastPointWorld._x;
                                    if (!_snapY) point._y = _lastPointWorld._y;
                                    if (!_snapZ) point._z = _lastPointWorld._z;

                                    lPoint = bone.InverseMatrix * point;

                                    Vector3 trans = (bone.FrameState._transform * lPoint - bone.FrameState._transform * _lastPointBone);

                                    if (trans._x != 0.0f) ApplyTranslation(0, trans._x);
                                    if (trans._y != 0.0f) ApplyTranslation(1, trans._y);
                                    if (trans._z != 0.0f) ApplyTranslation(2, trans._z);
                                    break;
                            }

                            _lastPointWorld = point;
                            _lastPointBone = bone.InverseMatrix * point;
                        }
                    }
                }
                else if (VertexLoc != null && GetVertexOrbPoint(new Vector2(e.X, e.Y), ((Vector3)VertexLoc), out point) && point != _lastPointWorld)
                {
                    //switch (_editType)
                    //{
                    //    case TransformType.Scale:

                    //        break;

                    //    case TransformType.Rotation:

                    //        Vector3 center = (Vector3)VertexLoc;
                    //        Matrix m = Matrix.AxisAngleMatrix(center, point);

                    //        Vector3 t = m.GetAngles();
                    //        Vector3 angles = t - _oldAngles;
                    //        _oldAngles = t;

                    //        if (angles._x > 180.0f) angles._x -= 360.0f;
                    //        if (angles._y > 180.0f) angles._y -= 360.0f;
                    //        if (angles._z > 180.0f) angles._z -= 360.0f;
                    //        if (angles._x < -180.0f) angles._x += 360.0f;
                    //        if (angles._y < -180.0f) angles._y += 360.0f;
                    //        if (angles._z < -180.0f) angles._z += 360.0f;

                    //        Matrix x = Matrix.RotationMatrix(angles);
                    //        foreach (Vertex3 vertex in _selectedVertices)
                    //        {
                    //            Vector3 pos = center + x * (vertex._weightedPosition - center);
                    //            if (pos != vertex._weightedPosition)
                    //                vertex.WeightedPosition = pos;
                    //        }
                    //        break;

                    //    case TransformType.Translation:
                            Vector3 trans = point - _lastPointWorld;
                            foreach (Vertex3 vertex in _selectedVertices)
                            {
                                Vector3 pos = vertex._weightedPosition;
                                if (_snapX) pos._x += trans._x;
                                if (_snapY) pos._y += trans._y;
                                if (_snapZ) pos._z += trans._z;
                                if (pos != vertex._weightedPosition)
                                    vertex.WeightedPosition = pos;
                            }
                    //        break;
                    //}

                    UpdateModel();
                    vertexEditor.UpdatePropDisplay();
                    _lastPointWorld = point;
                }
            }

            if (!moving && (!dontHighlightBonesAndVerticesToolStripMenuItem.Checked || (dontHighlightBonesAndVerticesToolStripMenuItem.Checked && ModelPanel.Selecting)))
                HighlightStuff(e);
        }
        public IBoneNode _hiBone = null;
        public Vertex3 _hiVertex = null;
        public void HighlightStuff(MouseEventArgs e)
        {
            ModelPanel.Cursor = Cursors.Default;
            float depth = ModelPanel.GetDepth(e.X, e.Y);

            _hiX = _hiY = _hiZ = _hiCirc = _hiSphere = false;
            
            IBoneNode bone = SelectedBone;

            if (bone != null)
            {
                //Get the location of the bone
                Vector3 center = BoneLoc;

                //Standard radius scaling snippet. This is used for orb scaling depending on camera distance.
                float radius = center.TrueDistance(ModelPanel.Camera.GetPoint()) / _orbRadius * 0.1f;

                if (_editType == TransformType.Rotation)
                {
                    //Get point projected onto our orb.
                    Vector3 point = ModelPanel.ProjectCameraSphere(new Vector2(e.X, e.Y), center, radius, false);

                    //Get the distance of the mouse point from the bone
                    float distance = point.TrueDistance(center);

                    if (Math.Abs(distance - radius) < (radius * _selectOrbScale)) //Point lies within orb radius
                    {
                        _hiSphere = true;

                        //Determine axis snapping
                        Vector3 angles = (bone.InverseMatrix * point).GetAngles() * Maths._rad2degf;
                        angles._x = (float)Math.Abs(angles._x);
                        angles._y = (float)Math.Abs(angles._y);
                        angles._z = (float)Math.Abs(angles._z);

                        if (Math.Abs(angles._y - 90.0f) <= _axisSnapRange)
                            _hiX = true;
                        else if (angles._x >= (180 - _axisSnapRange) || angles._x <= _axisSnapRange)
                            _hiY = true;
                        else if (angles._y >= (180 - _axisSnapRange) || angles._y <= _axisSnapRange)
                            _hiZ = true;
                    }
                    else if (Math.Abs(distance - (radius * _circOrbScale)) < (radius * _selectOrbScale)) //Point lies on circ line
                        _hiCirc = true;

                    if (_hiX || _hiY || _hiZ || _hiCirc)
                        ModelPanel.Cursor = Cursors.Hand;
                }
                else if (_editType == TransformType.Translation)
                {
                    Vector3 point = ModelPanel.UnProject(e.X, e.Y, depth);
                    Vector3 diff = (point - center) / radius;

                    float halfDist = _axisHalfLDist;
                    if (diff._x > -_axisSelectRange && diff._x < (_axisLDist + 0.01f) &&
                        diff._y > -_axisSelectRange && diff._y < (_axisLDist + 0.01f) &&
                        diff._z > -_axisSelectRange && diff._z < (_axisLDist + 0.01f))
                    {
                        //Point lies within axes
                        if (diff._x < halfDist && diff._y < halfDist && diff._z < halfDist)
                        {
                            //Point lies inside the double drag areas
                            if (diff._x > _axisSelectRange)
                                _hiX = true;
                            if (diff._y > _axisSelectRange)
                                _hiY = true;
                            if (diff._z > _axisSelectRange)
                                _hiZ = true;

                            ModelPanel.Cursor = Cursors.Hand;
                        }
                        else
                        {
                            //Check if point lies on a specific axis
                            float errorRange = _axisSelectRange;

                            if (diff._x > halfDist && Math.Abs(diff._y) < errorRange && Math.Abs(diff._z) < errorRange)
                                _hiX = true;
                            if (diff._y > halfDist && Math.Abs(diff._x) < errorRange && Math.Abs(diff._z) < errorRange)
                                _hiY = true;
                            if (diff._z > halfDist && Math.Abs(diff._x) < errorRange && Math.Abs(diff._y) < errorRange)
                                _hiZ = true;

                            if (!_hiX && !_hiY && !_hiZ)
                                goto GetBone;
                            else
                                ModelPanel.Cursor = Cursors.Hand;
                        }
                    }
                    else
                        goto GetBone;
                }
                else if (_editType == TransformType.Scale)
                {
                    Vector3 point = ModelPanel.UnProject(e.X, e.Y, depth);
                    Vector3 diff = (point - center) / radius;

                    if (diff._x > -_axisSelectRange && diff._x < (_axisLDist + 0.01f) &&
                        diff._y > -_axisSelectRange && diff._y < (_axisLDist + 0.01f) &&
                        diff._z > -_axisSelectRange && diff._z < (_axisLDist + 0.01f))
                    {
                        //Point lies within axes

                        //Check if point lies on a specific axis first
                        float errorRange = _axisSelectRange;

                        if (diff._x > errorRange && Math.Abs(diff._y) < errorRange && Math.Abs(diff._z) < errorRange)
                            _hiX = true;
                        if (diff._y > errorRange && Math.Abs(diff._x) < errorRange && Math.Abs(diff._z) < errorRange)
                            _hiY = true;
                        if (diff._z > errorRange && Math.Abs(diff._x) < errorRange && Math.Abs(diff._y) < errorRange)
                            _hiZ = true;

                        if (!_hiX && !_hiY && !_hiZ)
                        {
                            //Determine if the point is in the double or triple drag triangles
                            float halfDist = _scaleHalf2LDist;
                            float centerDist = _scaleHalf1LDist;
                            if (IsInTriangle(diff, new Vector3(), new Vector3(halfDist, 0, 0), new Vector3(0, halfDist, 0)))
                                if (IsInTriangle(diff, new Vector3(), new Vector3(centerDist, 0, 0), new Vector3(0, centerDist, 0)))
                                    _hiX = _hiY = _hiZ = true;
                                else _hiX = _hiY = true;
                            else if (IsInTriangle(diff, new Vector3(), new Vector3(halfDist, 0, 0), new Vector3(0, 0, halfDist)))
                                if (IsInTriangle(diff, new Vector3(), new Vector3(centerDist, 0, 0), new Vector3(0, 0, centerDist)))
                                    _hiX = _hiY = _hiZ = true;
                                else _hiX = _hiZ = true;
                            else if (IsInTriangle(diff, new Vector3(), new Vector3(0, halfDist, 0), new Vector3(0, 0, halfDist)))
                                if (IsInTriangle(diff, new Vector3(), new Vector3(0, centerDist, 0), new Vector3(0, 0, centerDist)))
                                    _hiX = _hiY = _hiZ = true;
                                else _hiY = _hiZ = true;

                            if (!_hiX && !_hiY && !_hiZ)
                                goto GetBone;
                            else
                                ModelPanel.Cursor = Cursors.Hand;
                        }
                        else
                            ModelPanel.Cursor = Cursors.Hand;
                    }
                    else
                        goto GetBone;
                }
            }

            //modelPanel1.Invalidate();

        GetBone:
            
            //Try selecting new bone
            //if (modelPanel._selecting)
            //{

            //}
            //else
            {
                if (!(_scaling || _rotating || _translating) && depth < 1.0f && _targetModel != null)
                {
                    IBoneNode o = null;

                    Vector3 point = ModelPanel.UnProject(e.X, e.Y, depth);

                    //Find orb near chosen point
                    if (EditingAll)
                    {
                        foreach (IModel m in _targetModels)
                            foreach (IBoneNode b in m.RootBones)
                                if (CompareDistanceRecursive(b, point, ref o))
                                    break;
                    }
                    else
                        foreach (IBoneNode b in _targetModel.RootBones)
                            if (CompareDistanceRecursive(b, point, ref o))
                                break;

                    if (_hiBone != null && _hiBone != SelectedBone)
                        _hiBone.NodeColor = Color.Transparent;
                    
                    if ((_hiBone = o) != null)
                    {
                        _hiBone.NodeColor = Color.FromArgb(255, 128, 0);
                        ModelPanel.Cursor = Cursors.Hand;
                    }
                }
                else if (_hiBone != null)
                {
                    if (_hiBone != SelectedBone)
                        _hiBone.NodeColor = Color.Transparent;
                    _hiBone = null;
                }
            }

            if (VertexLoc != null && RenderVertices)
            {
                //Get the location of the vertex
                Vector3 center = ((Vector3)VertexLoc);

                //Standard radius scaling snippet. This is used for orb scaling depending on camera distance.
                float radius = center.TrueDistance(ModelPanel.Camera.GetPoint()) / _orbRadius * 0.1f;

                //if (_editType == TransformType.Rotation)
                //{
                //    //Get point projected onto our orb.
                //    Vector3 point = ModelPanel.ProjectCameraSphere(new Vector2(e.X, e.Y), center, radius, false);

                //    //Get the distance of the mouse point from the bone
                //    float distance = point.TrueDistance(center);

                //    if (Math.Abs(distance - radius) < (radius * _selectOrbScale)) //Point lies within orb radius
                //    {
                //        _hiSphere = true;

                //        //Determine axis snapping
                //        Vector3 angles = center.LookatAngles(point) * Maths._rad2degf;
                //        angles._x = (float)Math.Abs(angles._x);
                //        angles._y = (float)Math.Abs(angles._y);
                //        angles._z = (float)Math.Abs(angles._z);

                //        if (Math.Abs(angles._y - 90.0f) <= _axisSnapRange)
                //            _hiZ = true;
                //        else if (angles._x >= (180 - _axisSnapRange) || angles._x <= _axisSnapRange)
                //            _hiY = true;
                //        else if (angles._y >= (180 - _axisSnapRange) || angles._y <= _axisSnapRange)
                //            _hiX = true;
                //    }
                //    else if (Math.Abs(distance - (radius * _circOrbScale)) < (radius * _selectOrbScale)) //Point lies on circ line
                //        _hiCirc = true;

                //    if (_hiX || _hiY || _hiZ || _hiCirc)
                //        ModelPanel.Cursor = Cursors.Hand;
                //}
                //else if (_editType == TransformType.Translation)
                //{
                    Vector3 point = ModelPanel.UnProject(e.X, e.Y, depth);
                    Vector3 diff = (point - center) / radius;

                    float halfDist = _axisHalfLDist;
                    if (diff._x > -_axisSelectRange && diff._x < (_axisLDist + 0.01f) &&
                        diff._y > -_axisSelectRange && diff._y < (_axisLDist + 0.01f) &&
                        diff._z > -_axisSelectRange && diff._z < (_axisLDist + 0.01f))
                    {
                        //Point lies within axes
                        if (diff._x < halfDist && diff._y < halfDist && diff._z < halfDist)
                        {
                            //Point lies inside the double drag areas
                            if (diff._x > _axisSelectRange)
                                _hiX = true;
                            if (diff._y > _axisSelectRange)
                                _hiY = true;
                            if (diff._z > _axisSelectRange)
                                _hiZ = true;

                            ModelPanel.Cursor = Cursors.Hand;
                        }
                        else
                        {
                            //Check if point lies on a specific axis
                            float errorRange = _axisSelectRange;

                            if (diff._x > halfDist && Math.Abs(diff._y) < errorRange && Math.Abs(diff._z) < errorRange)
                                _hiX = true;
                            if (diff._y > halfDist && Math.Abs(diff._x) < errorRange && Math.Abs(diff._z) < errorRange)
                                _hiY = true;
                            if (diff._z > halfDist && Math.Abs(diff._x) < errorRange && Math.Abs(diff._y) < errorRange)
                                _hiZ = true;

                            if (!_hiX && !_hiY && !_hiZ)
                                goto GetVertex;
                            else
                                ModelPanel.Cursor = Cursors.Hand;
                        }
                    }
                //}
            }

        GetVertex:

            //Try targeting a vertex
            if (RenderVertices)
            {
                if (ModelPanel.Selecting)
                {
                    if (NotCtrlAlt)
                        ResetVertexColors();

                    if (TargetModel.SelectedObjectIndex < 0)
                    {
                        foreach (IObject o in TargetModel.Objects)
                            if (o.IsRendering)
                                SelectVertices(o);
                    }
                    else
                    {
                        IObject w = TargetModel.Objects[TargetModel.SelectedObjectIndex];
                        if (w.IsRendering)
                            SelectVertices(w);
                        else
                            foreach (IObject h in TargetModel.Objects)
                                if (h.IsRendering)
                                    SelectVertices(h);
                    }
                }
                else
                {
                    Vector3 point = ModelPanel.UnProject(e.X, e.Y, depth);
                    if ((depth < 1.0f) && _targetModel != null)
                    {
                        Vertex3 v = null;

                        CompareVertexDistance(point, ref v);

                        if (_hiVertex != null && !_hiVertex._selected)
                        {
                            _hiVertex._highlightColor = Color.Transparent;
                            //ModelPanel.AllowSelection = true;
                        }
                        if ((_hiVertex = v) != null)
                        {
                            _hiVertex._highlightColor = Color.Orange;
                            ModelPanel.Cursor = Cursors.Cross;
                            //ModelPanel.AllowSelection = false;
                        }
                    }
                    else if (_hiVertex != null)
                    {
                        if (!_hiVertex._selected)
                        {
                            _hiVertex._highlightColor = Color.Transparent;
                            //ModelPanel.AllowSelection = true;
                        }
                        _hiVertex = null;
                    }
                }
            }
            
            ModelPanel.Invalidate();
        }
        private void SelectVertices(IObject o)
        {
            foreach (Vertex3 v in o.PrimitiveManager._vertices)
            {
                //Project each vertex into screen coordinates.
                //Then check to see if the 2D coordinates lie within the selection box.
                //In Soviet Russia, vertices come to YOUUUUU

                Vector3 worldPos = v.WeightedPosition;
                Vector2 screenPos = (Vector2)ModelPanel.Project(worldPos);
                Point start = ModelPanel.SelectionStart, end = ModelPanel.SelectionEnd;
                Vector2 min = new Vector2((float)Math.Min(start.X, end.X), (float)Math.Min(start.Y, end.Y));
                Vector2 max = new Vector2((float)Math.Max(start.X, end.X), (float)Math.Max(start.Y, end.Y));
                if ((screenPos <= max) && (screenPos >= min))
                    if (Alt)
                    {
                        v._selected = false;
                        if (_selectedVertices.Contains(v))
                            _selectedVertices.Remove(v);
                        v._highlightColor = Color.Transparent;
                    }
                    else if (!v._selected)
                    {
                        v._selected = true;

                        if (!Ctrl || !_selectedVertices.Contains(v))
                            _selectedVertices.Add(v);
                        v._highlightColor = Color.Orange;
                    }
            }
        }
        public void ResetBoneColors()
        {
            if (_targetModels != null)
                foreach (IModel m in _targetModels)
                    foreach (IBoneNode b in m.BoneCache)
                        b.BoneColor = b.NodeColor = Color.Transparent;
        }
        
        public List<Vertex3> _selectedVertices = new List<Vertex3>();
        public List<Vertex3> _temp = new List<Vertex3>();
        public void ResetVertexColors()
        {
            if (_targetModels != null)
                foreach (IModel m in _targetModels)
                    foreach (IObject o in m.Objects)
                        if (o.PrimitiveManager != null && o.PrimitiveManager._vertices != null)
                            foreach (Vertex3 v in o.PrimitiveManager._vertices)
                            {
                                v._highlightColor = Color.Transparent;
                                v._selected = false;
                            }
            _selectedVertices = new List<Vertex3>();
        }
        #endregion
    }
}
