using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Web;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;


namespace VKPlayer_Windows_
{

    public partial class MainWindow : Window
    {
        private List<Audio> _audioList;
        private readonly object _locker = new object();
        private int _currentIndex;
        private bool _isPlaying = false;
        private bool _isRepeat = false;
        private bool _isRandom = false;
        private int _audioCount;
        public MainWindow()
        {
            InitializeComponent();
        }
        #region Window
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            new Authorization().Show();

            Task task = new Task(GetUserAudio);
            task.ContinueWith((AddAudioToAudioView));
            task.Start();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion

        #region Media
        private void Player_MediaOpened(object sender, RoutedEventArgs e)
        {
            var audio = AudioView.SelectedItem as Audio;
            if (audio != null) FldNowPlay.Text = audio.artist + " - " + audio.title;
            StartProgressUpdater();
        }

        private void Player_MediaEnded(object sender, RoutedEventArgs e)
        {
            string url = null;
            _isPlaying = false;
            if (_isRepeat)
            {
                url = GetUrl(_currentIndex);
                PlayTrack(url);
                return;
            }

            if (_isRandom)
            {
                _currentIndex = new Random().Next(0, _audioCount);
                url = GetUrl(_currentIndex);
            }
            else
            {
                _currentIndex++;
                url = GetUrl(_currentIndex);

            }

            PlayTrack(url);
        }

        private void PlayTrack(string url)
        {
            Player.Source = new Uri(url);
            Player.Play();
            _isPlaying = true;
        }
        #endregion

        #region Button
        private void AudioView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _currentIndex = AudioView.SelectedIndex;
            PlayTrack(GetUrl(_currentIndex));
            PlayPauseImage();

        }

        private void ButRepeat_Click(object sender, RoutedEventArgs e)
        {
            Uri uri;
            if (!_isRepeat)
            {
                _isRepeat = true;
                uri = new Uri("pack://application:,,,/Resource/repeatOn.png");
            }
            else
            {
                _isRepeat = false;
                uri = new Uri("pack://application:,,,/Resource/repeatOff.png");
            }
            BitmapImage bitmap = new BitmapImage(uri);
            ImageRepeat.Source = bitmap;
        }

        private void ButPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                Player.Pause();
                _isPlaying = false;
                PlayPauseImage();
                return;
            }

            if (Player.Source == null && AudioView.SelectedIndex == -1)
            {
                MessageBox.Show("Не выбрана аудиозапись", "Уведомление", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;

            }
            if (Player.Source == null && AudioView.SelectedIndex != -1)
            {
                _currentIndex = AudioView.SelectedIndex;
                Player.Source = new Uri(GetUrl(_currentIndex));

            }
            Player.Play();
            _isPlaying = true;

            PlayPauseImage();
            StartProgressUpdater();
        }

        private void ButNext_Click(object sender, RoutedEventArgs e)
        {
            int index;
            if (_isRandom)
            {
                index = new Random().Next(0, _audioCount);
            }
            else
            {
                index = ++AudioView.SelectedIndex;
            }
            PlayTrack(GetUrl(index));
        }

        private void ButRandom_Click(object sender, RoutedEventArgs e)
        {
            Uri uri;
            if (!_isRandom)
            {
                _isRandom = true;
                uri = new Uri("pack://application:,,,/Resource/randomOn.png");
            }
            else
            {
                _isRandom = false;
                uri = new Uri("pack://application:,,,/Resource/randomOff.png");
            }

            BitmapImage bitmap = new BitmapImage(uri);
            ImageRandom.Source = bitmap;
        }

        private void ButPrevious_Click(object sender, RoutedEventArgs e)
        {
            _currentIndex = AudioView.SelectedIndex == 0 ? _audioCount : --AudioView.SelectedIndex;
            PlayTrack(GetUrl(_currentIndex));
        }

        private void ButSearch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FldRequest.Text))
            {
                return;
            }
            string request = FldRequest.Text;
            Task searchTask = new Task(SearchAudio, request);
            searchTask.Start();
            searchTask.ContinueWith(AddAudioToAudioView);
        }
        #endregion

        #region Вспомогательные методы
        private string GetUrl(int index)
        {
            AudioView.SelectedIndex = _currentIndex = index = index > _audioCount ? 0 : index;
            var audio = AudioView.Items[_currentIndex] as Audio;
            if (audio == null) GetUrl(index + 1);
            return GetTrueUrl(audio.url);
        }

        private string GetTrueUrl(string inputUrl)
        {
            return inputUrl.Substring(0, inputUrl.IndexOf('?'));
        }

        private void AddAudioToAudioView(Task obj)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()

            {
                AudioView.Items.Clear();
                _audioCount = _audioList.Count - 1;
                foreach (var item in _audioList)
                {
                    lock (_locker)
                    {
                        AudioView.Items.Add(item);

                    }
                }

            });
        }

        private void StartProgressUpdater()
        {
            Thread thread = new Thread(ProgressUpdater);
            thread.IsBackground = true;
            thread.Start();
        }

        private void ProgressUpdater()
        {
            while (_isPlaying)
            {
                Thread.Sleep(250);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { ProgressAudio.Value = Player.Position.TotalSeconds; });
            }
        }

        private void PlayPauseImage()
        {
            Uri uri;
            uri = _isPlaying ? new Uri("pack://application:,,,/Resource/pause.png") : new Uri("pack://application:,,,/Resource/play.png");
            BitmapImage bitmap = new BitmapImage(uri);
            ImagePlayPause.Source = bitmap;
        }

        #endregion

        #region Vk API
        private void GetUserAudio()
        {
            while (!AppSettings.Default.auth)
            {
                Thread.Sleep(500);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { ProgressAudio.IsIndeterminate = true; });
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () { ProgressAudio.IsIndeterminate = false; });

            var request = WebRequest.Create("https://api.vk.com/method/audio.get?owner_id=" + AppSettings.Default.id + "&need_user=0&access_token=" + AppSettings.Default.token);
            var response = request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            response.Close();
            responseFromServer = HttpUtility.HtmlDecode(responseFromServer);

            JToken token = JToken.Parse(responseFromServer);
            _audioList = token["response"].Children().Skip(1).Select(c => c.ToObject<Audio>()).ToList();
        }

        private void SearchAudio(object searchString)
        {
            string search = (string)searchString;

            var request = WebRequest.Create("https://api.vk.com/method/audio.search?q=" + search + "&auto_complete=1&count=50&access_token=" + AppSettings.Default.token);
            var response = request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            response.Close();
            responseFromServer = HttpUtility.HtmlDecode(responseFromServer);

            JToken token = JToken.Parse(responseFromServer);
            _audioList = token["response"].Children().Skip(1).Select(c => c.ToObject<Audio>()).ToList();

        }
        #endregion


    }
}
