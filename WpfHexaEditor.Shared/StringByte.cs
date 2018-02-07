﻿//////////////////////////////////////////////
// Apache 2.0  - 2016-2018
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributor: Janus Tida
//////////////////////////////////////////////

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexaEditor
{
    internal class StringByte : BaseByte
    {
        #region Global class variables

        private bool _tblShowMte = true;

        #endregion Global variable

        #region Contructor

        public StringByte(HexEditor parent) : base(parent)
        {
            //Default properties
            Width = 10;
        }
        #endregion Contructor

        #region Properties

        /// <summary>
        /// Next Byte of this instance (used for TBL/MTE decoding)
        /// </summary>
        public byte? ByteNext { get; set; }

        #endregion Properties

        #region Characters tables

        /// <summary>
        /// Show or not Multi Title Enconding (MTE) are loaded in TBL file
        /// </summary>
        public bool TblShowMte
        {
            get => _tblShowMte;
            set
            {
                _tblShowMte = value;
                UpdateTextRenderFromByte();
            }
        }

        /// <summary>
        /// Type of caracter table are used un hexacontrol.
        /// For now, somes character table can be readonly but will change in future
        /// </summary>
        public CharacterTableType TypeOfCharacterTable { get; set; }

        /// <summary>
        /// Custom character table
        /// </summary>
        public TblStream TblCharacterTable { get; set; }

        #endregion Characters tables

        #region Methods

        /// <summary>
        /// Update the render of text derived bytecontrol from byte property
        /// </summary>
        public override void UpdateTextRenderFromByte()
        {
            if (Byte != null)
            {
                switch (TypeOfCharacterTable)
                {
                    case CharacterTableType.Ascii:
                        Text = ByteConverters.ByteToChar(Byte.Value).ToString();
                        break;
                    case CharacterTableType.TblFile:
                        if (TblCharacterTable != null)
                        {
                            ReadOnlyMode = !TblCharacterTable.AllowEdit;

                            var content = "#";

                            if (TblShowMte && ByteNext.HasValue)
                            {
                                var mte = ByteConverters.ByteToHex(Byte.Value) +
                                          ByteConverters.ByteToHex(ByteNext.Value);
                                content = TblCharacterTable.FindMatch(mte, true);
                            }

                            if (content == "#")
                                content = TblCharacterTable.FindMatch(ByteConverters.ByteToHex(Byte.Value), true);

                            Text = content;
                        }
                        else
                            goto case CharacterTableType.Ascii;
                        break;
                }
            }
            else
                Text = string.Empty;
        }

        /// <summary>
        /// Update Background,foreground and font property
        /// </summary>
        public override void UpdateVisual()
        {
            if (IsSelected)
            {
                FontWeight = _parent.FontWeight;
                Foreground = _parent.ForegroundContrast;

                Background = FirstSelected ? _parent.SelectionFirstColor : _parent.SelectionSecondColor;
            }
            else if (IsHighLight)
            {
                FontWeight = _parent.FontWeight;
                Foreground = _parent.Foreground;
                Background = _parent.HighLightColor;
            }
            else if (Action != ByteAction.Nothing)
            {
                switch (Action)
                {
                    case ByteAction.Modified:
                        FontWeight = FontWeights.Bold;
                        Background = _parent.ByteModifiedColor;
                        Foreground = _parent.Foreground;
                        break;

                    case ByteAction.Deleted:
                        FontWeight = FontWeights.Bold;
                        Background = _parent.ByteDeletedColor;
                        Foreground = _parent.Foreground;
                        break;
                }
            }
            else
            {
                #region TBL COLORING

                FontWeight = _parent.FontWeight;
                Background = Brushes.Transparent;
                Foreground = _parent.Foreground;

                if (TypeOfCharacterTable == CharacterTableType.TblFile)
                    switch (Dte.TypeDte(Text))
                    {
                        case DteType.DualTitleEncoding:
                            Foreground = _parent.TbldteColor;
                            break;
                        case DteType.MultipleTitleEncoding:
                            Foreground = _parent.TblmteColor;
                            break;
                        case DteType.EndLine:
                            Foreground = _parent.TblEndLineColor;
                            break;
                        case DteType.EndBlock:
                            Foreground = _parent.TblEndBlockColor;
                            break;
                        default:
                            Foreground = _parent.TblDefaultColor;
                            break;
                    }

                #endregion
            }

            UpdateAutoHighLiteSelectionByteVisual();

            InvalidateVisual();
        }

        /// <summary>
        /// Render the control
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            //Draw background
            if (Background != null)
                dc.DrawRectangle(Background, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));

            //Draw text
            var typeface = new Typeface(_parent.FontFamily, _parent.FontStyle, FontWeight, _parent.FontStretch);

#if NET451
            var ft = new FormattedText(Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
                _parent.FontSize, Foreground);

#endif
#if NET47
            var ft = new FormattedText(Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
                _parent.FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
#endif
            dc.DrawText(ft, new Point(0, 0));

#region Update width of control 
            //It's 8-10 time more fastest to update width on render for TBL string
            switch (TypeOfCharacterTable)
            {
                case CharacterTableType.Ascii:
                    Width = 12;
                    break;
                case CharacterTableType.TblFile:
                    Width = ft.Width > 12 ? ft.Width : 12;
                    break;
            }
#endregion
        }

        /// <summary>
        /// Clear control
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            ByteNext = null;
        }

#endregion Methods

#region Events delegate

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Byte == null) return;

            if (KeyValidation(e)) return;

            //MODIFY ASCII...
            if (!ReadOnlyMode)
            {
                var isok = false;

                if (Keyboard.GetKeyStates(Key.CapsLock) == KeyStates.Toggled)
                {
                    if (Keyboard.Modifiers != ModifierKeys.Shift && e.Key != Key.RightShift && e.Key != Key.LeftShift)
                    {
                        Text = KeyValidator.GetCharFromKey(e.Key).ToString();
                        isok = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key != Key.RightShift &&
                             e.Key != Key.LeftShift)
                    {
                        isok = true;
                        Text = KeyValidator.GetCharFromKey(e.Key).ToString().ToLower();
                    }
                }
                else
                {
                    if (Keyboard.Modifiers != ModifierKeys.Shift && e.Key != Key.RightShift && e.Key != Key.LeftShift)
                    {
                        Text = KeyValidator.GetCharFromKey(e.Key).ToString().ToLower();
                        isok = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key != Key.RightShift &&
                             e.Key != Key.LeftShift)
                    {
                        isok = true;
                        Text = KeyValidator.GetCharFromKey(e.Key).ToString();
                    }
                }

                //Move focus event
                if (isok)
                {
                    Action = ByteAction.Modified;
                    Byte = ByteConverters.CharToByte(Text[0]);

                    //Insert byte at end of file
                    if (_parent.Lenght == BytePositionInFile + 1)
                    {
                        byte[] byteToAppend = { 0 };
                        _parent.AppendByte(byteToAppend);
                    }

                    OnMoveNext(new EventArgs());
                }
            }

            base.OnKeyDown(e);
        }

#endregion Events delegate

#region Caret events

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            _parent.HideCaret();
            base.OnLostFocus(e);
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (ReadOnlyMode || Byte == null)
                _parent.HideCaret();
            else
                _parent.MoveCaret(TransformToAncestor(_parent).Transform(new Point(0, 0)));

            base.OnGotFocus(e);
        }
#endregion
    }
}