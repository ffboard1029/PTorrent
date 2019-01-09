using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PTorrent
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.Multiselect = true;
            openFile.Title = "Select Torrent file";
            openFile.Filter = "Torrent|*.torrent|All files (*.*)|*.*";
            openFile.CheckFileExists = true;
            openFile.CheckPathExists = true;

            if (openFile.ShowDialog() ?? false)
            {
                var files = openFile.FileNames;
                if(files.Length > 0)
                {
                    foreach(var file in files)
                    {
                        var t = new Torrent(file);
                        if(t.ConstructionStatus)
                        {
                            //todo add to list of torrents, this will need to be in the main model (so the progres bar can be displayed)
                            Task.Run(() => BeginDownload(t));
                        }
                    }
                }
            }
        }

        private void BeginDownload(Torrent t)
        {
            t.ID = "P_T" + Path.GetRandomFileName().Replace(".", "") + DateTime.Now.ToString("mmssff");
            t.Port = 50000 + 1;

            t.UpdatePeerList(TrackerEventType.Started);
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
