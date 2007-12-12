//
// TrackActions.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using Mono.Unix;
using Gtk;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Playlist;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.Widgets;
using Banshee.Gui.Dialogs;

namespace Banshee.Gui
{
    public class TrackActions : ActionGroup
    {
        private InterfaceActionService action_service;
        private Dictionary<MenuItem, PlaylistSource> playlist_menu_map = new Dictionary<MenuItem, PlaylistSource> ();
        private RatingActionProxy rating_proxy;

        private static readonly string [] require_selection_actions = new string [] {
            "TrackContextMenuAction", "TrackPropertiesAction", "AddToPlaylistAction", "RemoveTracksAction", "RateTracksAction"
        };

        private IHasTrackSelection track_selector;
        public IHasTrackSelection TrackSelector {
            get { return track_selector; }
            set {
                if (track_selector != null && track_selector != value) {
                    track_selector.TrackSelectionProxy.Changed -= HandleSelectionChanged;
                    track_selector.TrackSelectionProxy.SelectionChanged -= HandleSelectionChanged;
                }

                track_selector = value;

                if (track_selector != null) {
                    track_selector.TrackSelectionProxy.Changed += HandleSelectionChanged;
                    track_selector.TrackSelectionProxy.SelectionChanged += HandleSelectionChanged;
                    Sensitize ();
                }
            }
        }

        public TrackActions (InterfaceActionService actionService) : base ("Track")
        {
            action_service = actionService;

            Add (new ActionEntry [] {
                new ActionEntry("TrackContextMenuAction", null, 
                    String.Empty, null, null, OnTrackContextMenu),

                new ActionEntry ("TrackPropertiesAction", Stock.Edit,
                    Catalog.GetString ("_Track Properties"), null,
                    Catalog.GetString ("Edit metadata on selected songs"), OnTrackProperties),

                new ActionEntry ("AddToPlaylistAction", Stock.Add,
                    Catalog.GetString ("Add _to Playlist"), null,
                    Catalog.GetString ("Append selected songs to playlist or create new playlist from selection"),
                    OnAddToPlaylist),

                new ActionEntry ("AddToNewPlaylistAction", Stock.New,
                    Catalog.GetString ("New Playlist"), null,
                    Catalog.GetString ("Create new playlist from selected tracks"),
                    OnAddToNewPlaylist),

                new ActionEntry ("RemoveTracksAction", Stock.Remove,
                    Catalog.GetString("_Remove"), "Delete",
                    Catalog.GetString("Remove selected song(s) from library"), OnRemoveTracks),

                new ActionEntry ("RateTracksAction", null,
                    String.Empty, null, null, OnRateTracks),
            });

            action_service.UIManager.ActionsChanged += HandleActionsChanged;

            action_service.GlobalActions["EditMenuAction"].Activated += HandleEditMenuActivated;

            this["AddToPlaylistAction"].HideIfEmpty = false;
        }

#region State Event Handlers

        private void HandleActionsChanged (object sender, EventArgs args)
        {
            if (action_service.UIManager.GetAction ("/MainMenu/EditMenu") != null) {
                rating_proxy = new RatingActionProxy (action_service.UIManager, this["RateTracksAction"]);
                rating_proxy.AddPath ("/MainMenu/EditMenu", "RemoveTracks");
                rating_proxy.AddPath ("/TrackContextMenu", "RemoveTracks");
                action_service.UIManager.ActionsChanged -= HandleActionsChanged;
            }
        }

        private void HandleSelectionChanged (object sender, EventArgs args)
        {
            Sensitize ();
        }

        private void HandleEditMenuActivated (object sender, EventArgs args)
        {
            ResetRating ();
        }

#endregion

#region Utility Methods

        private void Sensitize ()
        {
            Hyena.Collections.Selection selection = TrackSelector.TrackSelectionProxy.Selection;
            bool has_selection = (selection != null) && (selection.Count > 0);
            Sensitive = has_selection;
            //foreach (string action in require_selection_actions)
            //    this [action].Sensitive = has_selection;
        }

        private void ResetRating ()
        {
            bool first = true;
            int rating = 0;

            // If all the selected tracks have the same rating, show it
            foreach (TrackInfo track in TrackSelector.GetSelectedTracks ()) {
                if (track == null)
                    continue;

                if (first) {
                    rating = track.Rating;
                    first = false;
                } else if (track.Rating != rating) {
                    rating = 0;
                    break;
                }
            }
            rating_proxy.Reset (rating);
        }

#endregion
            
#region Action Handlers

        private void OnTrackContextMenu (object o, EventArgs args)
        {
            ResetRating ();

            Gtk.Menu menu = action_service.UIManager.GetWidget ("/TrackContextMenu") as Menu;
            menu.Show (); 
            menu.Popup (null, null, null, 0, Gtk.Global.CurrentEventTime);
        }

        private void OnTrackProperties (object o, EventArgs args)
        {
            TrackEditor propEdit = new TrackEditor (TrackSelector.GetSelectedTracks ());
            propEdit.Saved += delegate {
                //ui.playlistView.QueueDraw();
            };
        }

        // Called when the Add to Playlist action is highlighted.
        // Generates the menu of playlists to which you can add the selected tracks.
        private void OnAddToPlaylist (object o, EventArgs args)
        {
            Gdk.Pixbuf pl_pb = Gdk.Pixbuf.LoadFromResource ("source-playlist-16.png");
            playlist_menu_map.Clear ();
            Source active_source = ServiceManager.SourceManager.ActiveSource;

            // TODO find just the menu that was activated instead of modifying all proxies
            foreach (MenuItem menu in (o as Action).Proxies) {
                Menu submenu = new Menu ();
                menu.Submenu = submenu;

                submenu.Append (this ["AddToNewPlaylistAction"].CreateMenuItem ());
                submenu.Append (new SeparatorMenuItem ());
                foreach (Source child in ServiceManager.SourceManager.DefaultSource.Children) {
                    PlaylistSource playlist = child as PlaylistSource;
                    if (playlist != null) {
                        ImageMenuItem item = new ImageMenuItem (playlist.Name);
                        item.Image = new Gtk.Image (pl_pb);
                        item.Activated += OnAddToExistingPlaylist;
                        item.Sensitive = playlist != active_source;
                        playlist_menu_map[item] = playlist;
                        submenu.Append (item);
                    }
                }
                submenu.ShowAll ();
            }
        }

        private void OnAddToNewPlaylist (object o, EventArgs args)
        {
            // TODO generate name based on the track selection, or begin editing it
            PlaylistSource playlist = new PlaylistSource ("New Playlist");
            ServiceManager.SourceManager.DefaultSource.AddChildSource (playlist);

            ThreadAssist.Spawn (delegate {
                playlist.AddTracks (TrackSelector.TrackModel, TrackSelector.TrackSelectionProxy.Selection);
            });
        }

        private void OnAddToExistingPlaylist (object o, EventArgs args)
        {
            PlaylistSource playlist = playlist_menu_map[o as MenuItem];
            playlist.AddTracks (TrackSelector.TrackModel, TrackSelector.TrackSelectionProxy.Selection);
        }

        private void OnRemoveTracks (object o, EventArgs args)
        {
            Source source = ServiceManager.SourceManager.ActiveSource;

            if (source is PlaylistSource) {
                (source as PlaylistSource).RemoveTracks (TrackSelector.TrackSelectionProxy.Selection);
            }
        }

        private void OnRateTracks (object o, EventArgs args)
        {
            int rating = rating_proxy.LastRating;
            foreach (TrackInfo track in TrackSelector.GetSelectedTracks ()) {
                track.Rating = rating;
                track.Save ();
            }
        }

#endregion

    }
}
