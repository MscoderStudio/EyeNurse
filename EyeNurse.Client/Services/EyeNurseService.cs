﻿using Caliburn.Micro;
using EyeNurse.Client.Configs;
using EyeNurse.Client.Events;
using EyeNurse.Client.Helpers;
using EyeNurse.Client.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using JsonConfiger.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace EyeNurse.Client.Services
{
    public class EyeNurseService : INotifyPropertyChanged, IHandle<AppSettingChangedEvent>
    {
        Timer _timer;
        Setting _setting;
        IWindowManager _windowManager;
        LockScreenViewModel _lastLockScreenViewModel;
        TaskbarIcon _taskbarIcon;
        Icon _sourceIcon;
        readonly IEventAggregator _eventAggregator;
        bool warned;

        public EyeNurseService(IWindowManager windowManager, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            Init();
        }

        private async void Init()
        {
            _eventAggregator.Subscribe(this);
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Elapsed += Timer_Elapsed;

            //var rootDir = Environment.CurrentDirectory;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            ConfigFilePath = $"{appData}\\EyeNurse\\Configs\\setting.json";
            //DefaultConfigFilePath = Path.Combine(Environment.CurrentDirectory, "Configs\\default_config.json");

            await ResetCountDownAsync();
        }

        private async Task ResetCountDownAsync()
        {
            //休息结束
            if (_lastLockScreenViewModel != null)
            {
                _lastLockScreenViewModel.TryClose();
                _lastLockScreenViewModel = null;
            }

            _timer.Stop();
            IsResting = false;

            _setting = await JsonHelper.JsonDeserializeFromFileAsync<Setting>(ConfigFilePath);
            if (_setting == null || _setting.App == null)
            {
                //默认值
                _setting = new Setting()
                {
                    App = new AppSetting()
                    {
                        AlarmInterval = new TimeSpan(0, 45, 0),
                        RestTime = new TimeSpan(0, 3, 0)
                    }
                };
                await JsonHelper.JsonSerializeAsync(_setting, ConfigFilePath);
            }

            Countdown = _setting.App.AlarmInterval;
            CountdownPercent = 100;

            _timer.Start();
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsPaused)
            {
                PausedTime = PausedTime.Add(TimeSpan.FromMilliseconds(_timer.Interval));
                if (_taskbarIcon == null)
                {
                    _taskbarIcon = IoC.Get<TaskbarIcon>();
                    _sourceIcon = _taskbarIcon.Icon;
                }

                //todo 托盘显示暂停时间 https://github.com/MscoderStudio/EyeNurse/issues/4
                //using (Bitmap bm = new Bitmap(_taskbarIcon.Icon.ToBitmap()))
                //{
                //    var newIcon = Icon.FromHandle(bm.GetHicon());
                //    _taskbarIcon.Icon = newIcon;
                //}
            }
            else
            {
                PausedTime = new TimeSpan();
                if (!IsResting)
                {
                    Countdown = Countdown.Subtract(new TimeSpan(0, 0, 1));
                    CountdownPercent = Countdown.TotalSeconds / _setting.App.AlarmInterval.TotalSeconds * 100;
                    if (!warned && Countdown.TotalSeconds <= 30)
                    {
                        warned = true;
                        _eventAggregator.PublishOnUIThread(new PlayAudioEvent()
                        {
                            Source = @"Resources\Sounds\breakpre.mp3"
                        });
                    }
                    if (Countdown.TotalSeconds <= 0)
                    {
                        _timer.Stop();
                        _lastLockScreenViewModel = IoC.Get<LockScreenViewModel>();
                        RestTimeCountdown = _setting.App.RestTime;
                        Execute.OnUIThread(() =>
                        {
                            _windowManager.ShowWindow(_lastLockScreenViewModel);
                            _lastLockScreenViewModel.Deactivated += _lastLockScreenViewModel_Deactivated;
                        });
                        IsResting = true;
                        _timer.Start();
                    }
                }
                else
                {
                    warned = false;
                    RestTimeCountdown = RestTimeCountdown.Subtract(new TimeSpan(0, 0, 1));
                    RestTimeCountdownPercent = RestTimeCountdown.TotalSeconds / _setting.App.RestTime.TotalSeconds * 100;
                    if (RestTimeCountdown.TotalSeconds <= 0)
                    {
                        await ResetCountDownAsync();
                    }
                }
            }
        }

        private void _lastLockScreenViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            var temp = sender as LockScreenViewModel;
            temp.Deactivated -= _lastLockScreenViewModel_Deactivated;
            RestTimeCountdown = new TimeSpan();
        }

        #region properties

        #region IsResting

        /// <summary>
        /// The <see cref="IsResting" /> property's name.
        /// </summary>
        public const string IsRestingPropertyName = "IsResting";

        private bool _IsResting;

        /// <summary>
        /// IsResting
        /// </summary>
        public bool IsResting
        {
            get { return _IsResting; }

            set
            {
                if (_IsResting == value) return;

                _IsResting = value;

                if (value)
                    _eventAggregator.PublishOnUIThread(new PlayAudioEvent()
                    {
                        Source = @"Resources\Sounds\break.mp3"
                    });
                else
                    _eventAggregator.PublishOnUIThread(new PlayAudioEvent()
                    {
                        Source = @"Resources\Sounds\unlock.mp3"
                    });
                NotifyOfPropertyChange(IsRestingPropertyName);
            }
        }

        #endregion

        #region Countdown

        /// <summary>
        /// The <see cref="Countdown" /> property's name.
        /// </summary>
        public const string CountdownPropertyName = "Countdown";

        private TimeSpan _Countdown;

        /// <summary>
        /// Countdown
        /// </summary>
        public TimeSpan Countdown
        {
            get { return _Countdown; }

            set
            {
                if (_Countdown == value) return;

                _Countdown = value;
                NotifyOfPropertyChange(CountdownPropertyName);
            }
        }

        #endregion

        #region CountdownPercent

        /// <summary>
        /// The <see cref="CountdownPercent" /> property's name.
        /// </summary>
        public const string CountdownPercentPropertyName = "CountdownPercent";

        private double _CountdownPercent = 100;

        /// <summary>
        /// CountdownPercent
        /// </summary>
        public double CountdownPercent
        {
            get { return _CountdownPercent; }

            set
            {
                if (_CountdownPercent == value) return;

                _CountdownPercent = value;
                NotifyOfPropertyChange(CountdownPercentPropertyName);
            }
        }

        #endregion

        #region RestTimeCountdown

        /// <summary>
        /// The <see cref="RestTimeCountdown" /> property's name.
        /// </summary>
        public const string RestTimeCountdownPropertyName = "RestTimeCountdown";

        private TimeSpan _RestTimeCountdown;

        /// <summary>
        /// RestTimeCountdown
        /// </summary>
        public TimeSpan RestTimeCountdown
        {
            get { return _RestTimeCountdown; }

            set
            {
                if (_RestTimeCountdown == value) return;

                _RestTimeCountdown = value;
                NotifyOfPropertyChange(RestTimeCountdownPropertyName);
            }
        }

        #endregion

        #region RestTimeCountdownPercent

        /// <summary>
        /// The <see cref="RestTimeCountdownPercent" /> property's name.
        /// </summary>
        public const string RestTimeCountdownPercentPropertyName = "RestTimeCountdownPercent";

        private double _RestTimeCountdownPercent = 100;

        /// <summary>
        /// RestTimeCountdownPercent
        /// </summary>
        public double RestTimeCountdownPercent
        {
            get { return _RestTimeCountdownPercent; }

            set
            {
                if (_RestTimeCountdownPercent == value) return;

                _RestTimeCountdownPercent = value;
                NotifyOfPropertyChange(RestTimeCountdownPercentPropertyName);
            }
        }

        #endregion

        #region IsPaused

        /// <summary>
        /// The <see cref="IsPaused" /> property's name.
        /// </summary>
        public const string IsPausedPropertyName = "IsPaused";

        private bool _IsPaused;

        /// <summary>
        /// IsPaused
        /// </summary>
        public bool IsPaused
        {
            get { return _IsPaused; }

            set
            {
                if (_IsPaused == value) return;

                _IsPaused = value;
                NotifyOfPropertyChange(IsPausedPropertyName);
            }
        }

        #endregion

        #region PausedTime

        /// <summary>
        /// The <see cref="PausedTime" /> property's name.
        /// </summary>
        public const string PausedTimePropertyName = "PausedTime";

        private TimeSpan _PausedTime;

        /// <summary>
        /// PausedTime
        /// </summary>
        public TimeSpan PausedTime
        {
            get { return _PausedTime; }

            set
            {
                if (_PausedTime == value) return;

                _PausedTime = value;
                NotifyOfPropertyChange(PausedTimePropertyName);
            }
        }

        #endregion

        #endregion

        #region public methods

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resum()
        {
            IsPaused = false;
        }

        public void RestImmediately()
        {
            Countdown = new TimeSpan(0, 0, 1);
        }

        #region config

        //public async Task<T> LoadConfigAsync<T>(string path = null) where T : new()
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(path))
        //            path = GetConfigSavePath<T>();

        //        var config = await JsonHelper.JsonDeserializeFromFileAsync<T>(path);
        //        return config;
        //    }
        //    catch (Exception ex)
        //    {
        //        return default(T);
        //    }
        //}

        //public async Task SaveConfigAsync<T>(T data, string path = null) where T : new()
        //{
        //    if (string.IsNullOrEmpty(path))
        //        path = GetConfigSavePath<T>();

        //    var json = await JsonHelper.JsonSerializeAsync(data, path);
        //}

        //public string GetConfigSavePath<T>() where T : new()
        //{
        //    var rootDir = Environment.CurrentDirectory;
        //    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        //    return $"{appData}\\EyeNurse\\Configs\\{typeof(T).Name}.json";
        //}

        public string ConfigFilePath { get; private set; }
        public string DefaultConfigFilePath { get; private set; }

        #endregion

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyOfPropertyChange(string propertyName)
        {
            var handle = PropertyChanged;
            if (handle == null)
                return;
            handle(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public async void Handle(AppSettingChangedEvent message)
        {
            _setting = await JsonHelper.JsonDeserializeFromFileAsync<Setting>(ConfigFilePath);
        }
    }
}