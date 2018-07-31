﻿// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2018 AirVPN (support@airvpn.org) / https://airvpn.org
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>
//
// 20 June 2018 - author: promind - initial release. (a tribute to the 1859 Perugia uprising occurred on 20 June 1859 and in memory of those brave inhabitants who fought for the liberty of Perugia)

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Eddie.Common.Log;
using Eddie.Common.Threading;
using Java.Text;
using Java.Util;

namespace Eddie.NativeAndroidApp
{
    [Activity(Label = "Log Activity")]
    public class LogActivity : Activity
    {
        private List<string> logEntry = null;
        private ListView listLogView = null;
        private ImageButton btnShare = null;
        private ImageButton btnCommand = null;
        private long unixEpochTime = 0; // Unix rules forever. unixEpochTime is the beginning of everything. Dennis Ritchie, Ken Thompson and Brian Kernighan started the new and long lasting era of real and amazing computing. If computers, operating systems and mobile devices are what they are today, it certainly, clearly, unequivocally and undoubtedly is thanks to them all. Unix and C concepts are the foundation of everything and of modern computing. This is a fact, this is history. Long live Unix and C! [ProMIND]
        private LogListAdapter logListAdapter = null;

        private enum FormatType
        {
            HTML = 1,
            PLAIN_TEXT
        };

        private enum LogTime
        {
            UTC = 1,
            LOCAL
        };

        
        private class LogListAdapter : BaseAdapter
        {
            private List<string> logEntry = null;
            private LayoutInflater m_inflater = null;

            public LogListAdapter(LogActivity activity, List<string> entryList)
            {
                logEntry = entryList;
                m_inflater = LayoutInflater.From(activity);
            }

            public void DataSet(List<string> entryList)
            {
                logEntry = entryList;
                
                NotifyDataSetChanged();
            }
            
            public override int Count
            {
                get
                {
                    int entries = 0;
                    
                    if(logEntry != null)
                        entries = logEntry.Count;
                        
                    return entries;
                }
            }

            public override Java.Lang.Object GetItem(int position)
            {
                return logEntry[position];
            }

            public override long GetItemId(int position)
            {
                return position;
            }
    
            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                string item = GetItem(position).ToString();
                
                if(item == null)
                    return null;

                if(convertView == null)
                    convertView = m_inflater.Inflate(Resource.Layout.log_activity_listitem, null);

                convertView.Visibility = ViewStates.Visible;

                TextView txtLogEntry = convertView.FindViewById<TextView>(Resource.Id.log_entry);
                
                txtLogEntry.TextFormatted = Html.FromHtml(item); // deprecated method in order to support Android < 7.0

                return convertView;             
            }           
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.log_activity_layout);

            listLogView = FindViewById<ListView>(Resource.Id.log);

            unixEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks / TimeSpan.TicksPerSecond;

            btnCommand = FindViewById<ImageButton>(Resource.Id.btn_command);

            btnCommand.Click += delegate
            {
                ConsoleCommand();
            };

            btnShare = FindViewById<ImageButton>(Resource.Id.btn_share);

            btnShare.Click += delegate
            {
                SimpleDateFormat dateFormatter = null;
                Date logCurrenTimeZone = null;
                Calendar calendar = null;
                List<string> exportLog = null;
                string logText = "", logSubject = "";
                long utcTimeStamp = 0;

                calendar = Calendar.Instance;
                dateFormatter = new SimpleDateFormat("dd MMM yyyy HH:mm:ss");

                utcTimeStamp = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond) - unixEpochTime;
                calendar.TimeInMillis = utcTimeStamp * 1000;
                logCurrenTimeZone = (Date)calendar.Time;
                dateFormatter.TimeZone = Java.Util.TimeZone.GetTimeZone("GMT");

                logSubject = string.Format(Resources.GetString(Resource.String.log_subject), dateFormatter.Format(logCurrenTimeZone));

                logText = logSubject + "\n\nEddie for Android ";

                try
                {
                    string pkgName = Application.Context.ApplicationContext.PackageManager.GetPackageInfo(Application.Context.ApplicationContext.PackageName, 0).VersionName;
                    int pkgVersionCode = Application.Context.ApplicationContext.PackageManager.GetPackageInfo(Application.Context.ApplicationContext.PackageName, 0).VersionCode;

                    logText += string.Format("{0} Version Code {1}", pkgName, pkgVersionCode);
                }
                catch
                {
                    logText += "n.d.";
                }

                logText += "\n\n";

                exportLog = GetCurrentLog(FormatType.PLAIN_TEXT, LogTime.UTC);

                foreach(string entry in exportLog)
                    logText += entry + "\n";

                Intent shareIntent = new Intent(global::Android.Content.Intent.ActionSend);
                
                shareIntent.SetType("text/plain");
                shareIntent.PutExtra(global::Android.Content.Intent.ExtraTitle, Resources.GetString(Resource.String.log_title));
                shareIntent.PutExtra(global::Android.Content.Intent.ExtraSubject, logSubject);
                shareIntent.PutExtra(global::Android.Content.Intent.ExtraText, logText);
                
                StartActivity(Intent.CreateChooser(shareIntent, Resources.GetString(Resource.String.log_share_with)));
            };

            logEntry = GetCurrentLog(FormatType.HTML, LogTime.LOCAL);

            logListAdapter = new LogListAdapter(this, logEntry);
            
            listLogView.Adapter = logListAdapter;
        }
        
        private List<string> GetCurrentLog(FormatType formatType = FormatType.HTML, LogTime logTime = LogTime.UTC)
        {
            SimpleDateFormat dateFormatter = null;
            Date logCurrenTimeZone = null;
            Calendar calendar = null;
            List<string> logItem = null;
            long utcTimeStamp = 0;

            string log = "";

            calendar = Calendar.Instance;

            dateFormatter = new SimpleDateFormat("dd MMM yyyy HH:mm:ss");

            if(logTime == LogTime.UTC)
                dateFormatter.TimeZone = Java.Util.TimeZone.GetTimeZone("GMT");
            else
                dateFormatter.TimeZone = Java.Util.TimeZone.Default;

            using(DataLocker<List<LogEntry>> entries = LogsManager.Instance.Entries)
            {
                string logMsg;

                logItem = new List<string>();

                foreach(LogEntry entry in entries.Data)
                {
                    utcTimeStamp = (entry.Timestamp.Ticks / TimeSpan.TicksPerSecond) - unixEpochTime;

                    calendar.TimeInMillis = utcTimeStamp * 1000;
                    logCurrenTimeZone = (Date)calendar.Time;

                    switch(formatType)
                    {
                        case FormatType.HTML:
                        {                    
                            log = "<font color='blue'>" + dateFormatter.Format(logCurrenTimeZone) + "</font> [<font color='";
                            
                            switch(entry.Level)
                            {
                                case LogLevel.debug:
                                {
                                    log += "purple";
                                }
                                break;
        
                                case LogLevel.error:
                                {
                                    log += "red";
                                }
                                break;
        
                                case LogLevel.fatal:
                                {
                                    log += "red";
                                }
                                break;
        
                                case LogLevel.info:
                                {
                                    log += "darkgreen";
                                }
                                break;
        
                                case LogLevel.warning:
                                {
                                    log += "orange";
                                }
                                break;
                                
                                default:
                                {
                                    log += "black";
                                }
                                break;
                            }
                            
                            logMsg = entry.Message.Replace("\n", "<br>");

                            log += "'>"  + entry.Level + "</font>]: " + logMsg;
                        }
                        break;

                        case FormatType.PLAIN_TEXT:
                        {
                            log = dateFormatter.Format(logCurrenTimeZone) + " UTC [" + entry.Level +"] " + entry.Message;
                        }
                        break;

                        default:
                        {
                            log = "";
                        }
                        break;
                    }
                    
                    logItem.Add(log);
                }
            }
            
            return logItem;
        }
        
        void ConsoleCommand()
        {
            AlertDialog commandDialog = null;
            Button btnSend = null;
            Button btnCancel = null;

            Handler dialogHandler = new Handler(m => { throw new Java.Lang.RuntimeException(); });

            AlertDialog.Builder dialogBuilder = new AlertDialog.Builder(this);
    
            View content = LayoutInflater.From(this).Inflate(Resource.Layout.edit_option_dialog, null);

            EditText edtKey = content.FindViewById<EditText>(Resource.Id.key);

            edtKey.Text = "";

            btnSend = content.FindViewById<Button>(Resource.Id.btn_ok);
            btnCancel = content.FindViewById<Button>(Resource.Id.btn_cancel);

            btnSend.Text = Resources.GetString(Resource.String.send);

            btnSend.Enabled = true;
            btnCancel.Enabled = true;

            btnSend.Click += delegate
            {
                string commandResult = SendConsoleCommand(edtKey.Text);
                
                if(!commandResult.Equals(""))
                {
                    LogsManager.Instance.Debug(commandResult);
                    
                    logEntry = GetCurrentLog(FormatType.HTML, LogTime.LOCAL);
    
                    logListAdapter.DataSet(logEntry);
                }

                commandDialog.Dismiss();

                dialogHandler.SendMessage(dialogHandler.ObtainMessage());
            };

            btnCancel.Click += delegate
            {
                commandDialog.Dismiss();

                dialogHandler.SendMessage(dialogHandler.ObtainMessage());
            };

            dialogBuilder.SetTitle(Resources.GetString(Resource.String.console_command_title));
            dialogBuilder.SetView(content);

            commandDialog = dialogBuilder.Create();
            commandDialog.Show();
            
            try
            {
                Looper.Loop();
            }
            catch(Java.Lang.RuntimeException)
            {
            }
        }
        
        string SendConsoleCommand(string cmd)
        {
            return "";
        }
    }
}
