using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PTorrent
{
    public class Torrent : INotifyPropertyChanged
    {
        public event EventHandler<int> PieceVerified;

        public byte[] InfoHash { get; private set; } = new byte[20];
        public string HexStringInfohash { get { return BitConverter.ToString(InfoHash).Replace("-", ""); } }
        public string UrlSafeStringInfohash { get { return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(InfoHash, 0, 20)); } }
        public long TotalSize { get { return Files.Sum(x => x.Length); } }
        public string TotalSizeFormatted
        {
            get
            {
                //Bytes
                if (TotalSize < 1 << 10)
                {
                    return string.Format("{0} B", TotalSize);
                }
                //KiB
                else if (TotalSize < 1 << 20)
                {
                    return string.Format("{0:0.###} MiB", (double)TotalSize / (1 << 10));
                }
                //MiB
                else if (TotalSize < 1 << 30)
                {
                    return string.Format("{0:0.###} MiB", (double)TotalSize / (1 << 20));
                }
                //GiB
                else if (TotalSize < 1 << 40)
                {
                    return string.Format("{0:0.###} GiB", (double)TotalSize / (1 << 30));
                }
                //TiB
                else if (TotalSize < 1 << 50)
                {
                    return string.Format("{0:0.###} TiB", (double)TotalSize / (1 << 40));
                }
                //PiB   //I really hope not.....
                else
                {
                    return string.Format("{0:0.###} PiB", (double)TotalSize / (1 << 50));
                }
            }
        }

        public string Progress { get { return string.Format("{0:F1}%", NumVerifiedPieces / (double)PieceCount * 100); } }

        private object[] _fileLocks;

        private string _comment;
        private string _createdBy;
        private DateTime _creationDate;
        private string _name;
        private int _piecesLength;
        private List<TorrentFileItem> _files;
        private static SHA1 sha1 = SHA1.Create();
        private int _pieceCount;
        private string _downloadDirectory;

        public bool ConstructionStatus = false;

        /// <summary>
        /// The directory that is inside the torrent (if there are multiple files)
        /// </summary>
        public string DirectoryName
        {
            get { return Files.Count > 1 ? Name : ""; }
        }

        /// <summary>
        /// The directory that the user chooses as the final location to downlad to
        /// </summary>
        public string DownloadDirectory
        {
            get { return _downloadDirectory; }
            set { _downloadDirectory = value; NotifyPropertyChanged(); }
        }


        public bool? IsPrivate;

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

        /// <summary>
        /// Size of a block
        /// </summary>
        public int BlockSize { get; private set; } = 16384; //defaulted to 16KiB

        public int PieceLength
        {
            get { return _piecesLength; }
            set { _piecesLength = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The hashes for all of the pieces, 20 byte hash per piece, need to verify against these
        /// </summary>
        public byte[][] PieceHash { get; private set; }

        /// <summary>
        /// Count of the number of pieces, same as PiecesHash.Length
        /// </summary>
        public int PieceCount
        {
            get { return _pieceCount; }
            set { _pieceCount = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// The Pieces that we have successfully verified
        /// </summary>
        public bool[] VerifiedPieces { get; private set; }
        /// <summary>
        /// The blocks that we have already acquired
        /// </summary>
        public bool[][] AcquiredBlocks { get; private set; }

        /// <summary>
        /// Count of Verified Pieces
        /// </summary>
        public int NumVerifiedPieces { get { return VerifiedPieces.Count(x => x); } }

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

            if(!torrentDict.ContainsKey("announce"))
            {
                var t = new Tracker(Encoding.UTF8.GetString((byte[])torrentDict["announce"]));
                t.PeerListUpdated += HandleUpdatePeerList;
                Trackers.Add(t);
            }
            if(torrentDict.ContainsKey("announce-list"))
            {
                //todo not sure if this is always lists of lists with one element....
                var announceList = (List<object>)torrentDict["announce-list"];
                foreach(List<object> ann in announceList)
                {
                    var t = new Tracker(Encoding.UTF8.GetString((byte[])ann[0]));
                    t.PeerListUpdated += HandleUpdatePeerList;
                    Trackers.Add(t);
                }
            }

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

            _fileLocks = new object[Files.Count];

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

            PieceCount = Convert.ToInt32(TotalSize / (double)PieceLength);

            PieceHash = new byte[PieceCount][];
            VerifiedPieces = new bool[PieceCount];
            AcquiredBlocks = new bool[PieceCount][];


            for(int i = 0; i < PieceCount; i++)
            {
                PieceHash[i] = new byte[20];
                Buffer.BlockCopy(pieceHashes, i * 20, PieceHash[i], 0, 20);

                AcquiredBlocks[i] = new bool[GetBlockCount(i)];
            }

            var infoToHash = BEncoding.Encode(ConvertInfoToBEncode());
            InfoHash = sha1.ComputeHash(infoToHash);

            ConstructionStatus = true;
        }

        private void HandleUpdatePeerList(object sender, List<IPEndPoint> e)
        {

        }

        public object ConvertInfoToBEncode()
        {
            var infoDict = new Dictionary<string, object>();

            if (Files.Count > 1)
            {
                var bEncodefiles = new List<object>();
                foreach (var file in Files)
                {
                    var dict = new Dictionary<string, object>();
                    dict["length"] = file.Length;
                    dict["path"] = file.Path.Split(Path.DirectorySeparatorChar).Cast<object>().ToList();

                    bEncodefiles.Add(dict);
                }
                infoDict["files"] = bEncodefiles;
                infoDict["name"] = Name;
            }
            else
            {
                infoDict["length"] = Files[0].Length;
                infoDict["name"] = Files[0].Path;
            }

            if(IsPrivate != null)
            {
                infoDict["private"] = ((bool)IsPrivate) ? 1 : 0;
            }

            infoDict["piece length"] = PieceLength;
            infoDict["pieces"] = PieceHash.SelectMany(x => x).ToArray();

            return infoDict;
        }

        private int GetPieceSize(int pieceIndex)
        {
            if(pieceIndex == PieceCount - 1)
            {
                //shouldn't long % int always be an int.....? why have to cast?
                int remainder = (int)(TotalSize % PieceLength);
                if(remainder > 0)
                {
                    return remainder;
                }
            }
            return PieceLength;
        }

        private int GetBlockSize(int pieceIndex, int blockIndex)
        {
            if (blockIndex == GetBlockCount(pieceIndex) - 1)
            {
                //shouldn't long % int always be an int.....? why have to cast?
                int remainder = (int)(GetPieceSize(pieceIndex) / (double)BlockSize);
                if (remainder > 0)
                {
                    return remainder;
                }
            }
            return BlockSize;
        }

        private int GetBlockCount(int pieceIndex)
        {
            return Convert.ToInt32(Math.Ceiling(GetPieceSize(pieceIndex) / (double)BlockSize));
        }

        private byte[] Read(long startIndex, long length)
        {
            long end = startIndex + length;
            var data = new byte[length];

            for(int i = 0; i < Files.Count; i++)
            {
                if ((Files[i].End) < startIndex || Files[i].Offset > end)
                {
                    continue;
                }

                var path = Path.Combine(DownloadDirectory, DirectoryName, Files[i].Path);

                if(!File.Exists(path))
                {
                    return null;
                }

                long fstart = Math.Max(Files[i].Offset, startIndex) - Files[i].Offset;
                long fend = Math.Min(Files[i].End, end) - Files[i].Offset;
                long bstart = Math.Max(Files[i].Offset, startIndex) - startIndex;

                using (var fs = File.OpenRead(path))
                {
                    fs.Seek(fstart, SeekOrigin.Begin);
                    fs.Read(data, (int)bstart, (int)(fend - fstart));
                }
            }
            return data;
        }

        private byte[] ReadPiece(int pieceIndex)
        {
            return Read(pieceIndex * PieceLength, GetPieceSize(pieceIndex));
        }

        private byte[] ReadBlock(int pieceIndex, int blockIndex)
        {
            return Read((pieceIndex * PieceLength) + (blockIndex * BlockSize), GetBlockSize(pieceIndex, blockIndex));
        }

        private bool Write(long startIndex, byte[] data)
        {
            long end = startIndex + data.Length;
            for(int i = 0; i < Files.Count; i++)
            {
                if((Files[i].End) < startIndex || Files[i].Offset > end)
                {
                    continue;
                }

                var path = Path.Combine(DownloadDirectory, DirectoryName, Files[i].Path);

                var dir = Path.GetDirectoryName(path);
                try
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    lock (_fileLocks[i])
                    {
                        long fstart = Math.Max(Files[i].Offset, startIndex) - Files[i].Offset;
                        long fend = Math.Min(Files[i].End, end) - Files[i].Offset;
                        long bstart = Math.Max(Files[i].Offset, startIndex) - startIndex;

                        using (var fs = File.OpenWrite(path))
                        {
                            fs.Seek(fstart, SeekOrigin.Begin);
                            fs.Write(data, (int)bstart, (int)(fend - fstart));
                        }
                    }
                }
                catch(Exception ex)
                {
                    //probably file access error
                    return false;
                }
            }
            return true;
        }

        private bool WriteBlock(int pieceIndex, int blockIndex, byte[] data)
        {
            var result = Write((pieceIndex * PieceLength) + (blockIndex * BlockSize), data);
            if(result)
            {
                AcquiredBlocks[pieceIndex][blockIndex] = true;
                Verify(pieceIndex);
            }
            return result;
        }

        private void Verify(int pieceIndex)
        {
            var piece = ReadPiece(pieceIndex);
            var verified = false;
            if(piece != null)
            {
                var hash = sha1.ComputeHash(piece);
                verified = hash.SequenceEqual(PieceHash[pieceIndex]);
            }

            VerifiedPieces[pieceIndex] = verified;
            NotifyPropertyChanged("NumVerifiedPieces");

            if(verified)
            {
                for(int i = 0; i < AcquiredBlocks[pieceIndex].Length; i++)
                {
                    AcquiredBlocks[pieceIndex][i] = true;
                }
                PieceVerified?.Invoke(this, pieceIndex);
            }
            else if(AcquiredBlocks[pieceIndex].All(x => x))
            {
                for (int i = 0; i < AcquiredBlocks[pieceIndex].Length; i++)
                {
                    AcquiredBlocks[pieceIndex][i] = false;
                }
            }
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

        public long End { get{ return Offset + Length; } }

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
        public event EventHandler<List<IPEndPoint>> PeerListUpdated;

        private string _url;

        public Tracker(string url)
        {
            _url = url;
        }
    }
}
