using System;
using System.Windows;

namespace VKPlayer_Windows_
{

    public partial class Authorization : Window
    {
        public Authorization()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Browser.Navigate("https://oauth.vk.com/authorize?client_id=5420524&display=popup&redirect_uri=https://oauth.vk.com/blank.html&scope=audio&response_type=token&v=5.52&state");

        }
        private void Browser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            try
            {
                string url = Browser.Source.AbsoluteUri;
                string l = url.Split('#')[1];
                if (l[0] == 'a')
                {
                    AppSettings.Default.token = l.Split('&')[0].Split('=')[1];
                    AppSettings.Default.id = l.Split('=')[3];
                    AppSettings.Default.auth = true;
                    Close();
                }
            }
            catch (Exception exception)
            {

                MessageBox.Show(exception.Message);
            }

        }
    }


}

