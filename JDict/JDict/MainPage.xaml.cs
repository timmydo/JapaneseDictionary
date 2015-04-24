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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace JDict
{


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage Current;

        public MainPage()
        {
            this.InitializeComponent();
            MainPage.Current = this;
        }

        public enum NotifyType {
            StatusMessage,
            ErrorMessage
        };

        public void NotifyUser(string strMessage, NotifyType type) {
            switch (type) {
                // Use the status message style.
                case NotifyType.StatusMessage:
                    StatusBlock.Style = Resources["StatusStyle"] as Style;
                    break;
                // Use the error message style.
                case NotifyType.ErrorMessage:
                    StatusBlock.Style = Resources["ErrorStyle"] as Style;
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            if (StatusBlock.Text != String.Empty) {
                StatusBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            } else {
                StatusBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Called to append a list of suggestions to the SearchSuggestionCollection, if the prefix matches one in the suggestionList
        /// </summary>
        /// <param name="textToMatch">String to compare with suggestions in the suggestionList</param>
        /// <param name="e">Event when user submits query</param>
        private void AppendSuggestion(string textToMatch, SearchBoxSuggestionsRequestedEventArgs e, HashSet<string> existing) {
            App app = (App)Application.Current;
            app.db.InjectSuggestions(textToMatch, e.Request.SearchSuggestionCollection, existing);
        }

        
        private void TextSearch_SuggestionsRequested(SearchBox sender, SearchBoxSuggestionsRequestedEventArgs e)
        {
            HashSet<string> existingMatches = new HashSet<string>();
            
            if (!string.IsNullOrEmpty(e.QueryText)) {
                // If the string matches the query text shown in the search box, add suggestion to the search box.
                AppendSuggestion(e.QueryText, e, existingMatches);

                foreach (string alternative in e.LinguisticDetails.QueryTextAlternatives) {
                    // If the string matches against one of the query alternatives, add suggestion to the search box.
                    AppendSuggestion(alternative, e, existingMatches);
                }
            }

            if (e.Request.SearchSuggestionCollection.Size > 0) {
                MainPage.Current.NotifyUser("Suggestions provided for query: " + e.QueryText, NotifyType.StatusMessage);
            } else {
                App app = (App)Application.Current;
                MainPage.Current.NotifyUser("No suggestions provided for query: " + e.QueryText + ". Index size: " + app.db.IndexCount, NotifyType.StatusMessage);
            }
        }

        private void TextSearch_QuerySubmitted(SearchBox sender, SearchBoxQuerySubmittedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.QueryText)) {
                App app = (App)Application.Current;
                var matches = app.db.IndexMatchesForTerm(e.QueryText);
                HashSet<uint> offsetSet = new HashSet<uint>();
                var entries = new List<JDict.Dict.Entry>();
                foreach (var match in matches) {
                    for (int i = 0; i < match.matchOffsets.Length; i++) {
                        uint offset = match.matchOffsets[i];
                        if (offsetSet.Contains(offset)) {
                            continue;
                        }
                        offsetSet.Add(offset);
                        var entry = app.db.FetchEntry(offset);
                        entries.Add(entry);
                    }
                }

                entries.Sort(new JDict.Dict.EntryFrequencyComparer());

                ResultsGrid.ItemsSource = entries;
            }

        }

        private void ResultsGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) {
            EntryItem iv = args.ItemContainer.ContentTemplateRoot as EntryItem;

            if (args.InRecycleQueue == true) {
                iv.ClearData();
            } else if (args.Phase == 0) {
                iv.ShowPlaceholder(args.Item as JDict.Dict.Entry);

                // Register for async callback to visualize Title asynchronously
                args.RegisterUpdateCallback(ContainerContentChangingDelegate);
            } else if (args.Phase == 1) {
                iv.ShowTitle();
                args.RegisterUpdateCallback(ContainerContentChangingDelegate);
            } else if (args.Phase == 2) {
                iv.ShowTitle();

            }

            // For imporved performance, set Handled to true since app is visualizing the data item
            args.Handled = true;
        }

        /// <summary>
        /// Managing delegate creation to ensure we instantiate a single instance for 
        /// optimal performance. 
        /// </summary>
        private TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> ContainerContentChangingDelegate {
            get {
                if (_delegate == null) {
                    _delegate = new TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs>(ResultsGrid_ContainerContentChanging);
                }
                return _delegate;
            }
        }
        private TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> _delegate;
    }
}
