using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Universal_SMS_Archiver
{
    public class objSMSManager
    {
        public string BackingFolder { get; set; }
        public string BackingFileName { get; set; }
        public List<objSMS> SMS { get; set; }
        public List<objSMS> SMS_Archived { get; set; }

        public objSMSManager()
        {
            BackingFolder = Path.Combine(Path.GetDirectoryName(App.ResourceAssembly.Location), "Data");
            BackingFileName = Properties.Settings.Default.BackingFileName;
            SMS = new List<objSMS>();
        }

        public bool AddSMS(objSMS oSMS)
        {
            PostProcess(oSMS);
            foreach(var o in SMS.ToList())
            {
                if (o.ID == oSMS.ID)
                {
                    SMS.Remove(o);
                    SMS.Add(oSMS);
                    return false;
                }
            }

            SMS.Add(oSMS);

            return true;
        }

        public void InitializeBackingFileStore()
        {
            if (!Directory.Exists(BackingFolder))
                Directory.CreateDirectory(BackingFolder);

            if (File.Exists(Path.Combine(BackingFolder, BackingFileName)))
            {
                LoadFromBackingStore();
            }
            else
            {
                SMS.Add(new objSMS() {
                    Country = "US",
                    Date= DateTime.Now,
                    DateDelivered = null,
                    DateRead = DateTime.Now,
                    ID = Guid.NewGuid().ToString(),
                    Service = "Test",
                    Source = enumSMSSource.Others
                });

                SaveToBackingStore();
            }
        }

        void PostProcess(objSMS theSMS)
        {
            if (theSMS.MobileNumber != null)
            {
                if (Properties.Settings.Default.TrimMobileNumber)
                {
                    //+\d\d\d\d
                    if (theSMS.MobileNumber.Length > 1)
                    {
                        int i = 1;
                        while (i < theSMS.MobileNumber.Length)
                        {
                            if (!Char.IsLetterOrDigit(theSMS.MobileNumber[i]))
                            {
                                theSMS.MobileNumber = theSMS.MobileNumber.Remove(i, 1);
                                i--;
                            }
                            i++;
                        }
                    }
                }

                if (!theSMS.MobileNumber.StartsWith("+"))
                {
                    //must check that the subsequent numbers are all digits before applying country code

                    bool Proceed = true;
                    foreach(var c in theSMS.MobileNumber)
                    {
                        if (!Char.IsDigit(c))
                        {
                            Proceed = false;
                            break;
                        }
                    }

                    if (Proceed)
                        theSMS.MobileNumber = Properties.Settings.Default.DefaultCountryCode + theSMS.MobileNumber;
                }
            }
        }

        public void LoadFromBackingStore()
        {
            SMS = new List<objSMS>();

            if (File.Exists(Path.Combine(BackingFolder, BackingFileName)))
            {
                SMS = Newtonsoft.Json.JsonConvert.DeserializeObject<List<objSMS>>(File.ReadAllText(Path.Combine(BackingFolder, BackingFileName)));
                foreach (var o in SMS)
                {
                    PostProcess(o);
                }
            }

            SMS_Archived = new List<objSMS>();

            if (File.Exists(Path.Combine(BackingFolder, Properties.Settings.Default.ArchiveBackingFileName)))
            {
                SMS_Archived = Newtonsoft.Json.JsonConvert.DeserializeObject<List<objSMS>>(File.ReadAllText(Path.Combine(BackingFolder, Properties.Settings.Default.ArchiveBackingFileName)));
            }
        }
 
        public void SaveToBackingStore()
        {
            var DestinationFileName = Path.Combine(BackingFolder, BackingFileName);
            var BackupFileName = Path.Combine(BackingFolder, Path.GetFileNameWithoutExtension(BackingFileName) + ".bak");
            if (File.Exists(BackupFileName))
                File.Delete(BackupFileName);

            if (File.Exists(DestinationFileName))
                File.Copy(DestinationFileName, BackupFileName);

            File.WriteAllText(DestinationFileName, Newtonsoft.Json.JsonConvert.SerializeObject((from p in SMS
                                                                                                orderby p.MobileNumber, p.Date
                                                                                                select p), Newtonsoft.Json.Formatting.Indented));
        }

        public void AddToArchiveStore(IEnumerable<objSMS> lSMS)
        {
            var aSMS = new List<objSMS>();

            var BackingFile = Path.Combine(BackingFolder, Properties.Settings.Default.ArchiveBackingFileName);
            if (File.Exists(BackingFile))
                aSMS = Newtonsoft.Json.JsonConvert.DeserializeObject<List<objSMS>>(File.ReadAllText(Path.Combine(BackingFolder, Properties.Settings.Default.ArchiveBackingFileName)));

            foreach(var o in lSMS)
            {
                var o2 = (objSMS)o;
                o2.DateArchived = DateTime.Now;
                aSMS.Add(o2);

                var SMSRec = SMS.Where(p => p.ID == o.ID).FirstOrDefault();
                if (SMSRec != null)
                    SMS.Remove(SMSRec);
            }

            var dSMS = new Dictionary<string, objSMS>();
            foreach(var o in aSMS)
            {
                if (!dSMS.ContainsKey(o.ID))
                    dSMS.Add(o.ID, o);
            }

            File.WriteAllText(BackingFile, Newtonsoft.Json.JsonConvert.SerializeObject((from p in dSMS.Values
                                                                                        orderby p.MobileNumber, p.Date
                                                                                                select p), Newtonsoft.Json.Formatting.Indented));

            SaveToBackingStore();
            SMS_Archived = Newtonsoft.Json.JsonConvert.DeserializeObject<List<objSMS>>(File.ReadAllText(Path.Combine(BackingFolder, Properties.Settings.Default.ArchiveBackingFileName)));
        }
    }

    public class objSMS
    {
        public string MobileNumber { get; set; }
        public string Message { get; set; }

        public DateTime Date { get; set; }
        public DateTime? DateRead { get; set; }
        public DateTime? DateDelivered { get; set; }
        /// <summary>
        /// ID of the message, varies depending on whether it is from iTunes or SMS Backup and Restore
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Country where the SMS comes from, iOS stores this
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        /// Source of the message, either iTunes or SMS backup and restore
        /// </summary>
        public enumSMSSource Source { get; set; }

        /// <summary>
        /// Service where this comes from, iTunes specific
        /// </summary>
        public string Service { get; set; }

        /// <summary>
        /// Where did this data come from
        /// </summary>
        public string ImportSource { get; set; }
        /// <summary>
        /// When was this data imported
        /// </summary>
        public DateTime ImportDate { get; set; }
        public DateTime? DateArchived { get; set; }
    }

    public enum enumSMSSource
    {
        iTunes,
        SMSBackupAndRestore,
        Others
    }
}
