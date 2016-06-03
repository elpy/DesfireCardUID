using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.SmartCards;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TestUWPProject
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SmartCardReader Reader = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            OutputTextBlock.Text = "Check for NFC support...";

            try
            {
                if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Devices.SmartCards.SmartCardConnection"))
                {
                    Debug.WriteLine("SmartCardConnection supported");
                    OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. SmartCards are supported.";

                    var devSelector = SmartCardReader.GetDeviceSelector(SmartCardReaderKind.Nfc);
                    var devices = await DeviceInformation.FindAllAsync(devSelector);
                    Reader = await SmartCardReader.FromIdAsync(devices.FirstOrDefault().Id);
                    Reader.CardAdded += CardReader_CardAdded;

                    Debug.WriteLine("Ready to connect");
                    OutputTextBlock.Text = $"{OutputTextBlock.Text}\nBring your card...";
                }
                else
                {
                    Debug.WriteLine("SmartCardConnection supported");
                    OutputTextBlock.Text = $"{OutputTextBlock.Text}\nFailed.SmartCards aren't supported.";
                }
            }
            catch(Exception e0)
            {
                await new MessageDialog($"Exception: {e0}").ShowAsync();
                OutputTextBlock.Text = $"{OutputTextBlock.Text}\nFailed. Exception:{e0}";
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (Reader != null)
                Reader.CardAdded -= CardReader_CardAdded;

            base.OnNavigatedFrom(e);
        }

        private async void CardReader_CardAdded(SmartCardReader sender, CardAddedEventArgs args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Debug.WriteLine("Card added");
                OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. Got card. Processing...";

                var card = args.SmartCard;

                try
                {
                    using (var conn = await card.ConnectAsync())
                    {
                        var cmdh = new byte[] { 0x90, 0x60, 0x00, 0x00, 0x00 }; //Hardware related data
                        var cmds = new byte[] { 0x90, 0xAF, 0x00, 0x00, 0x00 }; //Software related data
                        var cmdu = new byte[] { 0x90, 0xAF, 0x00, 0x00, 0x00 }; //Card UID

                        var resh = await Send(conn, cmdh.AsBuffer());
                        Debug.WriteLine($"Hardware info: {ByteArrayToHEXString(resh.ToArray())}");
                        OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. Hardware info: {ByteArrayToHEXString(resh.ToArray())}";

                        if (IsThereMoreInfo(resh.ToArray()))
                        {
                            Debug.WriteLine("There is more info. Processing...");
                            OutputTextBlock.Text = $"{OutputTextBlock.Text}\nThere is more info. Processing...";

                            var ress = await Send(conn, cmds.AsBuffer());
                            Debug.WriteLine($"Ok. Software info: {ByteArrayToHEXString(ress.ToArray())}");
                            OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. Software info: {ByteArrayToHEXString(ress.ToArray())}";

                            if (IsThereMoreInfo(ress.ToArray()))
                            {
                                Debug.WriteLine("There is more info. Processing...");
                                OutputTextBlock.Text = $"{OutputTextBlock.Text}\nThere is more info. Processing...";

                                var resu = await Send(conn, cmdu.AsBuffer());
                                Debug.WriteLine($"Ok. Card info: {ByteArrayToHEXString(resu.ToArray())}");
                                OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. Card info: {ByteArrayToHEXString(resu.ToArray())}. Extract UID...";

                                //1-8 bytes - wanted UID
                                if (resu.Length > 8)
                                {
                                    var uid = resu.ToArray(1, 7);
                                    Debug.WriteLine($"Ok. Card UID: {ByteArrayToHEXString(uid)}");
                                    OutputTextBlock.Text = $"{OutputTextBlock.Text}\nOk. Card UID: {ByteArrayToHEXString(uid)}. Extract UID...";
                                }
                                else
                                {
                                    Debug.WriteLine("Failed to extract card uid.");
                                    OutputTextBlock.Text = $"{OutputTextBlock.Text}\nFailed to extract card uid.";
                                }
                            }
                            else
                            {
                                Debug.WriteLine("No more info. Strange...");
                                OutputTextBlock.Text = $"{OutputTextBlock.Text}\nNo more info. Strange...";
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No more info. Strange...");
                            OutputTextBlock.Text = $"{OutputTextBlock.Text}\nNo more info. Strange...";
                        }

                        Debug.WriteLine("Connection closed.");
                        OutputTextBlock.Text = $"{OutputTextBlock.Text}\nConnection closed.";
                    }
                }
                catch (Exception e0)
                {
                    Debug.WriteLine($"Failed to execute command: {e0}");
                    OutputTextBlock.Text = OutputTextBlock.Text + $"\nFailed to execute command: {e0}";
                }
            });
        }

        private bool IsThereMoreInfo(byte[] array)
        {
            if (array.Length > 2 && array[array.Length - 2] == 0x91 && array[array.Length - 1] == 0xAF)
                return true;

            return false;
        }

        private async Task<IBuffer> Send(SmartCardConnection connection, IBuffer request)
        {
            var res = await connection.TransmitAsync(request);
            return res;
        }

        private string ByteArrayToHEXString(byte[] array)
        {
            var hex = new StringBuilder();
            foreach (var b in array)
                hex.AppendFormat("{0:x2} ", b);
            return hex.ToString();
        }
    }
}
