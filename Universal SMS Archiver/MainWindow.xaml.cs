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
using System.IO;
using System.Data.SQLite;
using System.Data;
using System.Xml;
using OfficeOpenXml;

namespace Universal_SMS_Archiver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        objSMSManager SMSManager = new objSMSManager();

        public MainWindow()
        {
            InitializeComponent();
            SMSManager.InitializeBackingFileStore();
            RefreshGrid();
            lbl.Content = $"Current SMS: {SMSManager.SMS.Count}, Archived SMS: {SMSManager.SMS_Archived.Count}";
        }

        private void Menu_File_Exit_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void Menu_File_Import_SMS_Backup_And_Restore_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "SMS Backup and Restore files (*.xml)|*.xml|All Files (*.*)|*.*" };
            var result = ofd.ShowDialog();
            if (result == false) return;
            lbl.Content = ofd.FileName;

            var doc = new XmlDocument();
            doc.Load(ofd.FileName);

            int Processed_Total = 0;
            int Processed_New = 0;
            
            foreach (XmlNode xn in doc.SelectNodes("/smses/sms"))
            {
                var oSMS = new objSMS()
                {
                    Country = "",
                    Date = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(xn.Attributes["date"].Value)).UtcDateTime,
                    DateDelivered = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64((xn.Attributes["date_sent"].Value.Length > 0 ? xn.Attributes["date_sent"].Value : "0"))).UtcDateTime,
                    DateRead = null,
                    ID = xn.Attributes["address"].Value + ":" + xn.Attributes["date_sent"].Value,
                    ImportDate = DateTime.Now,
                    ImportSource = ofd.FileName,
                    Message = xn.Attributes["body"].Value,
                    MobileNumber = xn.Attributes["address"].Value,
                    Service = xn.Attributes["protocol"].Value + ":" + xn.Attributes["type"].Value,
                    Source = enumSMSSource.SMSBackupAndRestore
                };
                oSMS.ID += oSMS.Service;

                Processed_Total++;
                if (SMSManager.AddSMS(oSMS))
                    Processed_New++;

                if (Processed_Total % 1000 == 0)
                    SMSManager.SaveToBackingStore();
            }

            SMSManager.SaveToBackingStore();
            lbl.Content = $"Import from iTunes backup completed, processed {Processed_Total}, {Processed_New} new records inserted";
            RefreshGrid();
        }

        private void Menu_File_Import_iTunes_Backup_Click(object sender, RoutedEventArgs e)
        {
            lbl.Content = "Import from iTunes backup initiated";

            var allSMSFiles = new List<FileInfo>();
            var PathToITunesBackup = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Apple Computer\MobileSync\Backup");
            foreach (var d in Directory.EnumerateDirectories(PathToITunesBackup))
            {
                if (File.Exists(System.IO.Path.Combine(d, "3d0d7e5fb2ce288813306e4d4636395e047a3d28")))
                {
                    allSMSFiles.Add(new FileInfo(System.IO.Path.Combine(d, "3d0d7e5fb2ce288813306e4d4636395e047a3d28")));
                }
            }

            lbl.Content += " - " + allSMSFiles.Count + " files to process";
            int Processed_Total = 0;
            int Processed_New = 0;

            foreach (var f in allSMSFiles.OrderBy(p => p.CreationTime))
            {
                string dbConnection = $"Data Source={f.FullName}";
                using (var cnn = new SQLiteConnection(dbConnection))
                {
                    cnn.Open();
                    using (var cmd = new SQLiteCommand(cnn))
                    {
                        cmd.CommandText = @"
SELECT 
message.guid as ID, 
handle.country as Country, 
message.service as Service,
handle.id as MobileNumber,
message.text as Message,
datetime(message.date, 'unixepoch', '+31 years', '+0 hours') as Date,
case message.date_read when message.date_read=0 then
	datetime(0, 'unixepoch')
else
	datetime(message.date_read, 'unixepoch', '+31 years', '+0 hours')
end as DateRead,

case message.date_delivered when message.date_delivered=0 then
	datetime(0, 'unixepoch')
else
	datetime(message.date_delivered, 'unixepoch', '+31 years', '+0 hours')
end as DateDelivered
FROM message, handle WHERE message.handle_id = handle.ROWID
order by message.guid
";
                        var reader = cmd.ExecuteReader();
                        using (var tbl = new DataTable())
                        {
                            tbl.Load(reader);
                            DG.ItemsSource = tbl.Rows;

                            foreach (DataRow row in tbl.Rows)
                            {
                                var oSMS = new objSMS()
                                {
                                    Country = row["Country"].ToString(),
                                    Date = Convert.ToDateTime(row["Date"]),
                                    DateDelivered = Convert.ToDateTime(row["DateDelivered"]),
                                    DateRead = Convert.ToDateTime(row["DateRead"]),
                                    ID = row["ID"].ToString(),
                                    ImportDate = DateTime.Now,
                                    ImportSource = "iTunes-" + f.FullName,
                                    Message = row["Message"].ToString(),
                                    MobileNumber = row["MobileNumber"].ToString(),
                                    Service = row["Service"].ToString(),
                                    Source = enumSMSSource.iTunes
                                };

                                oSMS.ID = oSMS.MobileNumber + oSMS.ID;

                                if (oSMS.DateDelivered.Value.Year == 2001)
                                    oSMS.DateDelivered = null;

                                if (oSMS.DateRead.Value.Year == 2001)
                                    oSMS.DateRead = null;

                                Processed_Total++;
                                if (SMSManager.AddSMS(oSMS))
                                    Processed_New++;
                            }
                        }
                        reader.Close();
                    }
                }
            }

            SMSManager.SaveToBackingStore();
            lbl.Content = $"Import from iTunes backup completed, processed {Processed_Total}, {Processed_New} new records inserted";
            RefreshGrid();
        }

        void RefreshGrid()
        {
            DG.ItemsSource = (from p in SMSManager.SMS
                              orderby p.MobileNumber, p.Date
                              select p);

            DG_Archived.ItemsSource = (from p in SMSManager.SMS_Archived
                              orderby p.MobileNumber, p.Date
                              select p);
        }

        private void Menu_File_Export_As_Excel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.SaveFileDialog() { Filter = "Microsoft Excel (*.xlsx)|*.xlsx",FileName = "sms.xlsx" };
            var result = ofd.ShowDialog();
            if (result == false) return;
            lbl.Content = ofd.FileName;

            if (File.Exists(ofd.FileName))
                File.Delete(ofd.FileName);

            using (var pck = new ExcelPackage(new FileInfo(ofd.FileName)))
            {
                var ws = pck.Workbook.Worksheets.Add("SMS");
                ws.Cells["A1"].LoadFromCollection(SMSManager.SMS, true);
                pck.Save();
            }

            MessageBox.Show($"Successfully exported to {ofd.FileName}");
        }

        private void DG_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var ItemsToDelete = DG.SelectedItems.Cast<objSMS>();
                if (MessageBox.Show($"Are you sure you want to archive these records?\nTotal: {ItemsToDelete.Count()} SMS records to be archived.", "Confirm Deletion", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    SMSManager.AddToArchiveStore(ItemsToDelete);
                    RefreshGrid();

                    lbl.Content = $"{ItemsToDelete.Count()} sms moved to archive. Current SMS: {SMSManager.SMS.Count}, Archived SMS: {SMSManager.SMS_Archived.Count}";
                }
            }
        }

        private void Menu_File_Reload_From_Disk_Click(object sender, RoutedEventArgs e)
        {
            SMSManager.LoadFromBackingStore();
        }
    }
}