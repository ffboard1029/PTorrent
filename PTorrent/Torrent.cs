using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PTorrent
{
    public class Torrent : INotifyPropertyChanged
    {
        public byte[] Infohash { get; private set; } = new byte[20];
        public string HexStringInfohash { get { return BitConverter.ToString(Infohash).Replace("-", ""); } }
        public string UrlSafeStringInfohash { get { return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20)); } }
        public long TotalSize { get { return Files.Sum(x => x.Length); } }

        private string _comment;
        private string _createdBy;
        private DateTime _creationDate;
        private string _name;
        private int _piecesLength;
        private List<TorrentFileItem> _files;

        public bool ConstructionStatus = false;

        public string DirectoryName
        {
            get { return Files.Count > 1 ? Name : ""; }
        }

        public bool IsPrivate;

        public string Comment
        {
            get { return _comment; }
            set { _comment = value; NotifyPropertyChanged(); }
        }

        public string CreatedBy
        {
            get { return _createdBy; }
            set { _createdBy = value; NotifyPropertyChanged(); }
        }

        public DateTime CreationDate
        {
            get { return _creationDate; }
            set { _creationDate = value; NotifyPropertyChanged(); }
        }

        public Encoding Encoding { get; private set; }

        public string Name
        {
            get { return _name; }
            set { _name = value; NotifyPropertyChanged(); }
        }

        public int PieceLength
        {
            get { return _piecesLength; }
            set { _piecesLength = value; NotifyPropertyChanged(); }
        }

        public byte[][] PieceHash { get; private set; }

        public List<TorrentFileItem> Files
        {
            get { return _files; }
            set { _files = value; NotifyPropertyChanged(); }
        }

        public List<Tracker> Trackers { get; } = new List<Tracker>();


        public Torrent(string torrentFilePath)
        {
            if(!BEncoding.DecodeFile(torrentFilePath, out object decodedTorrentFile))
            {
                return;
            }

            if(!(decodedTorrentFile is Dictionary<string, object>))
            {
                return;
            }
            var torrentDict = (Dictionary<string, object>)decodedTorrentFile;

            if(torrentDict.ContainsKey("comment"))
            {
                Comment = Encoding.UTF8.GetString((byte[])torrentDict["comment"]);
            }

            if (torrentDict.ContainsKey("created by"))
            {
                CreatedBy = Encoding.UTF8.GetString((byte[])torrentDict["created by"]);
            }

            if (torrentDict.ContainsKey("creation date"))
            {
                var epoch = new DateTime(1970, 1, 1);
                CreationDate = epoch.AddSeconds((long)torrentDict["creation date"]);
            }

            if(torrentDict.ContainsKey("encoding"))
            {
                Encoding = Encoding.GetEncoding(Encoding.UTF8.GetString((byte[])torrentDict["encoding"]));
            }

            if(!torrentDict.ContainsKey("info"))
            {
                return;
            }
            var info = (Dictionary<string, object>)torrentDict["info"];

            if(!info.ContainsKey("files"))
            {
                return;
            }
            
            long totalLength = 0;
            foreach(var file in (List<object>)info["files"])
            {
                var fileItem = new TorrentFileItem();

                if(!(file is Dictionary<string, object>))
                {
                    continue;
                }
                var fileDict = (Dictionary<string, object>)file;

                if(!fileDict.ContainsKey("length"))
                {
                    continue;
                }
                fileItem.Length = (long)fileDict["length"];
                fileItem.Offset = totalLength;
                totalLength += fileItem.Length;
                
                if(!fileDict.ContainsKey("path"))
                {
                    continue;
                }
                var paths = (List<object>)fileDict["path"];
                fileItem.Path = string.Join(Path.DirectorySeparatorChar.ToString(), paths.Select(x => Encoding.UTF8.GetString((byte[])x)).ToArray());
                Files.Add(fileItem);
            }
            if(Files.Count < 1)
            {
                return;
            }

            if(info.ContainsKey("private"))
            {
                IsPrivate = (int)torrentDict["private"] == 1;
            }

            if(!info.ContainsKey("piece length"))
            {
                return;
            }
            PieceLength = (int)info["piece length"];

            if(!info.ContainsKey("pieces"))
            {
                return;
            }
            var pieceHashes = (byte[])info["pieces"];
            PieceHash = new byte[PieceLength][];

            ConstructionStatus = true;
        }

        #region NotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class TorrentFileItem : INotifyPropertyChanged
    {
        private long _length;
        private string _path;
        public long Offset;


        public string Path
        {
            get { return _path; }
            set { _path = value; NotifyPropertyChanged(); }
        }

        public long Length
        {
            get { return _length; }
            set { _length = value; NotifyPropertyChanged(); }
        }

        public string FormattedSize
        {
            get { return BEncoding.EncodeNumber(Length); }
        }


        #region NotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class Tracker
    {
        private string _url;

        public Tracker(string url)
        {
            _url = url;
        }
    }
}
