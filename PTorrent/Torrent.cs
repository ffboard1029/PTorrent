using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PTorrent
{
    public class Torrent : INotifyPropertyChanged
    {
        public List<Tracker> Trackers { get; private set; }
        private string _comment;
        private string _createdBy;

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

        private DateTime _creationDate;

        public DateTime CreationDate
        {
            get { return _creationDate; }
            set { _creationDate = value; NotifyPropertyChanged(); }
        }

        public Encoding Encoding { get; private set; }
        



        #region NotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class TorrentFile : INotifyPropertyChanged
    {
        private long _length;
        public long Offset;
        private string _path;

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
            get { return string.Format("6:lengthi{0}e", Length); }
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
