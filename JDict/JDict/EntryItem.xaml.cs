using JDict.Dict;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace JDict {
    public sealed partial class EntryItem : UserControl {
        private Entry _item;

        public EntryItem() {
            this.InitializeComponent();
        }

        public void ShowPlaceholder(Entry item) {
            _item = item;

            kanjiTextBlock.Opacity = 0;
            readingsTextBlock.Opacity = 0;
            sensesTextBlock.Opacity = 0;
        }


        public void ShowTitle() {
            if (this._item.kanji.Count > 0) {
                string k = this._item.kanji.Select((x) => x.text).Aggregate((i, j) => i + ", " + j);
                kanjiTextBlock.Text = k;
                kanjiTextBlock.Opacity = 1;
            } else {
                kanjiTextBlock.ClearValue(TextBlock.TextProperty);
                kanjiTextBlock.Opacity = 0;
            }

            if (this._item.readings.Count > 0) {
                string r = this._item.readings.Select((x) => x.text).Aggregate((i, j) => i + ", " + j);
                readingsTextBlock.Text = r;
                readingsTextBlock.Opacity = 1;
            } else {
                readingsTextBlock.ClearValue(TextBlock.TextProperty);
                readingsTextBlock.Opacity = 0;
            }


            if (this._item.senses.Count > 0) {
                StringWriter sw = new StringWriter();
                foreach (var s in this._item.senses) {
                    if (s.glosses.Count > 0) {
                        sw.WriteLine(s.glosses.Aggregate((i, j) => i + ", " + j));
                    }
                }

                sensesTextBlock.Text = sw.ToString();

                sensesTextBlock.Opacity = 1;
            } else {
                sensesTextBlock.ClearValue(TextBlock.TextProperty);
                sensesTextBlock.Opacity = 0;
            }


        }




        /// <summary>
        /// Drop all refrences to the data item
        /// </summary>
        public void ClearData() {
            _item = null;
            kanjiTextBlock.ClearValue(TextBlock.TextProperty);
            readingsTextBlock.ClearValue(TextBlock.TextProperty);
            sensesTextBlock.ClearValue(TextBlock.TextProperty);
        }

    }
}
