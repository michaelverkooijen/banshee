using System;
using System.Collections.Generic;
using System.Xml;
using System.Text;

namespace MusicBrainzSharp
{
    # region Enums

    public enum ReleaseType
    {
        Album,
        Audiobook,
        Compilation,
        EP,
        Interview,
        Live,
        None,
        Other, 
        Remix,
        Single,
        Soundtrack,
        Spokenword,
    }

    public enum ReleaseStatus
    {
        Bootleg,
        None,
        Official,
        Promotion,
        PsudoRelease
    }

    public enum ReleaseFormat
    {
        Cartridge,
        Casette,
        CD,
        DAT,
        Digital,
        DualDisc,
        DVD,
        LaserDisc,
        MiniDisc,
        None,
        Other,
        ReelToReel,
        SACD,
        Vinyl
    }

    public enum ReleaseIncType
    {
        // Object
        ArtistRels = 0,
        LabelRels = 1,
        ReleaseRels = 2,
        TrackRels = 3,
        UrlRels = 4,

        // Item
        Artist = 6,
        TrackLevelRels = 7,
        
        // Release
        Counts = 8,
        Discs = 9,
        Labels = 10,
        ReleaseEvents = 11,
        Tracks = 12

    }

    #endregion

    public sealed class ReleaseInc : Inc
    {
        public ReleaseInc(ReleaseIncType type)
            : base((int)type)
        {
            name = EnumUtil.EnumToString(type);
        }

        public static implicit operator ReleaseInc(ReleaseIncType type)
        {
            return new ReleaseInc(type);
        }
    }

    public sealed class ReleaseQueryParameters : ItemQueryParameters
    {
        string disc_id;
        public string DiscId
        {
            get { return disc_id; }
            set { disc_id = value; }
        }

        string date;
        public string Date
        {
            get { return date; }
            set { date = value; }
        }

        string asin;
        public string Asin
        {
            get { return asin; }
            set { asin = value; }
        }

        string language;
        public string Language
        {
            get { return language; }
            set { language = value; }
        }

        string script;
        public string Script
        {
            get { return script; }
            set { script = value; }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if(disc_id != null) {
                builder.Append("&discid=");
                builder.Append(disc_id);
            }
            if(date != null) {
                builder.Append("&date=");
                AppendStringToBuilder(builder, date);
            }
            if(asin != null) {
                builder.Append("&asin=");
                builder.Append(asin);
            }
            if(language != null) {
                builder.Append("&language=");
                builder.Append(language);
            }
            if(script != null) {
                builder.Append("&script=");
                builder.Append(script);
            }
            AppendBaseToBuilder(builder);
            return builder.ToString();
        }
    }

    public sealed class Release : MusicBrainzItem
    {
        const string EXTENSION = "release";
        protected override string url_extension { get { return EXTENSION; } }

        public static ReleaseInc[] DefaultIncs = new ReleaseInc[] { };
        protected override Inc[] default_incs
        {
            get { return DefaultIncs; }
        }
        
        Release(string mbid, params Inc[] incs)
            : base(mbid, incs)
        {
            bool dont_attempt_labels = false;
            foreach(Inc inc in incs)
                switch(inc.Value) {
                case (int)ReleaseIncType.Counts:
                    dont_attempt_disc_count = true;
                    break;
                case (int)ReleaseIncType.Discs:
                    dont_attempt_discs = true;
                    break;
                case (int)ReleaseIncType.ReleaseEvents:
                    dont_attempt_events = true;
                    break;
                case (int)ReleaseIncType.Labels:
                    dont_attempt_labels = true;
                    break;
                case (int)ReleaseIncType.Tracks:
                    dont_attempt_tracks = true;
                    break;
                }
            dont_attempt_event_label = dont_attempt_events && dont_attempt_labels;
        }

        internal Release(XmlReader reader)
            : base(reader)
        {
        }

        protected override bool ProcessAttributes(XmlReader reader)
        {
            // How sure am I about getting the type and status in the "Type Status" format?
            // MB really ought to specify these two things seperatly.
            string type_string = reader.GetAttribute("type");
            if(type_string != null) {
                foreach(string token in type_string.Split(' ')) {
                    if(this.type == ReleaseType.None) {
                        bool found = false;
                        foreach(ReleaseType type in Enum.GetValues(typeof(ReleaseType)) as ReleaseType[])
                            if(EnumUtil.EnumToString(type) == token) {
                                this.type = type;
                                found = true;
                                break;
                            }
                        if(found)
                            continue;
                    }

                    foreach(ReleaseStatus status in Enum.GetValues(typeof(ReleaseStatus)) as ReleaseStatus[])
                        if(EnumUtil.EnumToString(status) == token) {
                            this.status = status;
                            break;
                        }
                }
            }
            return this.type != ReleaseType.None || this.status != ReleaseStatus.None;
        }

        protected override bool ProcessXml(XmlReader reader)
        {
            reader.Read();
            bool result = base.ProcessXml(reader);
            if(!result) {
                result = true;
                switch(reader.Name) {
                case "text-representation":
                    language = reader.GetAttribute("language");
                    script = reader.GetAttribute("script");
                    break;
                case "asin":
                    reader.Read();
                    asin = reader.ReadContentAsString();
                    break;
                case "disc-list": {
                        string count = reader.GetAttribute("count");
                        if(count != null)
                            disc_count = int.Parse(count);
                        else {
                            if(reader.ReadToDescendant("disc")) {
                                discs = new List<Disc>();
                                do discs.Add(new Disc(reader.ReadSubtree()));
                                while(reader.ReadToNextSibling("disc"));
                                disc_count = discs.Count;
                            }
                        }
                        break;
                    }
                case "release-event-list":
                    if(reader.ReadToDescendant("event")) {
                        events = new List<Event>();
                        do events.Add(new Event(reader.ReadSubtree(), this));
                        while(reader.ReadToNextSibling("event"));
                    }
                    break;
                case "track-list": {
                        string offset = reader.GetAttribute("offset");
                        if(offset != null)
                            track_number = int.Parse(offset) + 1;
                        string count = reader.GetAttribute("count");
                        if(count != null)
                            track_count = int.Parse(count);
                        if(reader.ReadToDescendant("track")) {
                            tracks = new List<Track>();
                            do tracks.Add(new Track(reader.ReadSubtree()));
                            while(reader.ReadToNextSibling("track"));
                            track_count = tracks.Count;
                        }
                        break;
                    }
                case "track-level-rels":
                    break;
                default:
                    result = false;
                    break;
                }
            }
            reader.Close();
            return result;
        }

        #region Properties

        ReleaseType type = ReleaseType.None;
        public ReleaseType Type
        {
            get { return type; }
        }

        ReleaseStatus status = ReleaseStatus.None;
        public ReleaseStatus Status
        {
            get { return status; }
        }

        string language = string.Empty;
        public string Language
        {
            get { return language; }
        }

        string script = string.Empty;
        public string Script
        {
            get { return script; }
        }

        string asin = string.Empty;
        public string Asin
        {
            get { return asin; }
        }

        List<Disc> discs;
        bool dont_attempt_discs;
        public List<Disc> Discs
        {
            get {
                if(discs == null)
                    discs = dont_attempt_discs
                        ? new List<Disc>()
                        : new Release(MBID, ReleaseIncType.Discs).Discs;
                return discs;
            }
        }

        int? disc_count;
        bool dont_attempt_disc_count;
        public int DiscCount
        {
            get {
                if(!disc_count.HasValue)
                    disc_count = dont_attempt_disc_count
                        ? 0
                        : new Release(MBID, ReleaseIncType.Counts).DiscCount;
                return disc_count.Value;
            }
        }

        List<Event> events;
        bool dont_attempt_events;
        public List<Event> Events
        {
            get {
                if(events == null)
                    events = dont_attempt_events
                        ? new List<Event>()
                        : new Release(MBID, ReleaseIncType.ReleaseEvents).Events;
                return events;
            }
        }

        bool dont_attempt_event_label;
        internal void GetEventLabel()
        {
            if(dont_attempt_event_label)
                return;
            Release release = new Release(MBID, ReleaseIncType.ReleaseEvents, ReleaseIncType.Labels);
            for(int i = 0; i < Events.Count; i++ ) {
                Events[i].Label = release.Events[i].Label;
            }
            dont_attempt_event_label = true;
        }

        List<Track> tracks;
        bool dont_attempt_tracks;
        public List<Track> Tracks
        {
            get {
                if(tracks == null)
                    tracks = dont_attempt_tracks
                        ? new List<Track>()
                        : new Release(MBID, ReleaseIncType.Tracks).Tracks;
                return tracks; 
            }
        }

        int? track_number;
        public int TrackNumber
        {
            get { return track_number.HasValue ? track_number.Value : -1; }
        }

        int? track_count;
        internal int TrackCount
        {
            get {
                if(!track_count.HasValue)
                    track_count = dont_attempt_tracks
                    ? 0
                    : new Release(MBID, ReleaseIncType.Tracks).TrackCount;
                return track_count.Value;
            }
        }

        #endregion

        #region Get

        public static Release Get(string mbid)
        {
            return Get(mbid, (Inc[])DefaultIncs);
        }

        public static Release Get(string mbid, params ReleaseInc[] incs)
        {
            return Get(mbid, (Inc[])incs);
        }

        static Release Get(string mbid, params Inc[] incs)
        {
            return new Release(mbid, incs);
        }

        protected override MusicBrainzObject ConstructObject(string mbid, params Inc[] incs)
        {
            return Get(mbid, incs);
        }

        #endregion

        #region Query

        public static Query<Release> Query(string title)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, ReleaseStatus status)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ReleaseStatus = status;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, ReleaseStatus status, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ReleaseStatus = status;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, string artist)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.Artist = artist;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, string artist, ReleaseStatus status)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.Artist = artist;
            parameters.ReleaseStatus = status;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, string artist, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.Artist = artist;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, string artist, ReleaseStatus status, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.Artist = artist;
            parameters.ReleaseStatus = status;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, Artist artist)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ArtistId = artist.MBID;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, Artist artist, ReleaseStatus status)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseStatus = status;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, Artist artist, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(string title, Artist artist, ReleaseStatus status, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.Title = title;
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseStatus = status;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(Artist artist, ReleaseStatus status)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseStatus = status;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(Artist artist, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(Artist artist, ReleaseStatus status, ReleaseType type)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.ArtistId = artist.MBID;
            parameters.ReleaseStatus = status;
            parameters.ReleaseType = type;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(Disc disc)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.DiscId = disc.Id;
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(ReleaseQueryParameters parameters)
        {
            return Query<Release>(EXTENSION, parameters);
        }

        public static Query<Release> Query(ReleaseQueryParameters parameters, byte limit)
        {
            return Query<Release>(EXTENSION, limit, 0, parameters);
        }

        public static Query<Release> QueryLucene(string lucene_query)
        {
            return Query<Release>(EXTENSION, lucene_query);
        }

        public static Query<Release> QueryLucene(string lucene_query, byte limit)
        {
            return Query<Release>(EXTENSION, limit, 0, lucene_query);
        }

        public static Query<Release> QueryFromDisc(string device)
        {
            ReleaseQueryParameters parameters = new ReleaseQueryParameters();
            parameters.DiscId = Disc.GetFromDevice(device).Id;
            return Query<Release>(EXTENSION, parameters);
        }

        #endregion
    }
}
