using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
                            //todo add to list of torrents, this will need to be a model
                        }
                    }
                }
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
