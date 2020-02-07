using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Identity.Client;

namespace active_directory_wpf_msgraph_v2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        readonly string[] _scopes = { "https://lannertestfuncapp.azurewebsites.net/user_impersonation" };

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Call AcquireToken - to acquire a token requiring user to sign-in
        /// </summary>
        private async void CallFuncButton_Click(object sender, RoutedEventArgs e)
        {
            AuthenticationResult authResult = null;
            var app = App.PublicClientApp;
            ResultText.Text = string.Empty;
            TokenInfoText.Text = string.Empty;

            var accounts = await app.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            try
            {
                authResult = await app.AcquireTokenSilent(_scopes, firstAccount)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent. 
                // This indicates you need to call AcquireTokenInteractive to acquire a token
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    authResult = await app.AcquireTokenInteractive(_scopes)
                        .WithAccount(firstAccount)
                        .WithParentActivityOrWindow(new WindowInteropHelper(this).Handle) // optional, used to center the browser on the window
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                }
                catch (MsalException msalex)
                {
                    ResultText.Text = $"Error Acquiring Token:{Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently:{Environment.NewLine}{ex}";
                return;
            }

            if (authResult != null)
            {
                // Call Azure function
                const string functionUrl = "https://lannertestfuncapp.azurewebsites.net/api/TestFunc1?code=2YlqQiydOInqeP6mcAsaAVgcf94xdgzRclpP3Qv26jIU0DS7I3vpAQ==&name=TestName";
                ResultText.Text = await GetHttpContentWithToken(functionUrl, authResult.AccessToken);
        
                DisplayBasicTokenInfo(authResult);
                SignOutButton.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Perform an HTTP GET request to a URL using an HTTP Authorization header
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="token">The token</param>
        /// <returns>String containing the results of the GET operation</returns>
        public async Task<string> GetHttpContentWithToken(string url, string token)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    ServicePointManager.SecurityProtocol =
                        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    return content;
                }
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        /// <summary>
        /// Sign out the current user
        /// </summary>
        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var accounts = await App.PublicClientApp.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount != null)
            {
                try
                {
                    await App.PublicClientApp.RemoveAsync(firstAccount);
                    ResultText.Text = "User has signed-out";
                    CallFuncButton.Visibility = Visibility.Visible;
                    SignOutButton.Visibility = Visibility.Collapsed;
                }
                catch (MsalException ex)
                {
                    ResultText.Text = $"Error signing-out user: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// Display basic information contained in the token
        /// </summary>
        private void DisplayBasicTokenInfo(AuthenticationResult authResult)
        {
            TokenInfoText.Text = "";
            if (authResult != null)
            {
                TokenInfoText.Text += $"Username: {authResult.Account.Username}" + Environment.NewLine;
                TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
            }
        }
    }
}
