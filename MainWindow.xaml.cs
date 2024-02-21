using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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

namespace Marking_Control
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool isSelfTestMode = false;
        public bool isConnected = false;
        public bool isCodeLoaded = false;
        public bool isCodeLoadingActive = false;
        public bool isPrintSimulationActive = false;

        public TcpClient tcpClient;
        public NetworkStream stream;
        public StreamReader reader;
        public StreamWriter writer;

        public string host = "192.168.10.20";
        public int port = 222;

        public List<string> codes = new List<string>();

        public int codesLoaded = 0;
        public int codesInQueue = 0;
        readonly public int codesInQueueMax = 20;

        public List<string> markingCodesImported;   // Пул загруженных кодов, чтобы помнить, что изначально загрузили
        public Queue<string> markingCodesSource;    // Очередь кодов на печать, откуда пополняем очередь принтера
        public Queue<string> markingCodesQueued;    // Очередь для отслеживания текущей очереди принтера 
        public Queue<string> markingCodePrinted;    // Очередь кодов отправленных на печать для последующей проверки
        public List<string> markingCodeVeryfied;    // Пул напечатанных и проверенных кодов
        public List<string> markingCodesDiscarded;  // Пул отбракованных кодов

        public Task printSimulationTask;
        public CancellationTokenSource printSimulationTaskCancellationTokenSource;
        public CancellationToken printSimulationTaskCancellationToken;
        public Task codeLoadingTask;
        public CancellationTokenSource codeLoadingTaskCancellationTokenSource;
        public CancellationToken codeLoadingTaskCancellationToken;

        #region Строковые переменные
        // Кнопка подключения и статус подключения
        readonly public string uiDisconnectButtonText = "Отключить";
        readonly public string uiConnectButtonText = "Подключить";
        readonly public string uiConnectionStatusOnlineText = "Подключено";
        readonly public string uiConnectionStatusOfflineText = "Отключено";

        // Кнопка загрузки кодов и статус загрузки кодов
        readonly public string uiCodeLoadingButtonText = "Загрузить коды";
        readonly public string uiCodeLoadigStatusLoeadedText = "Коды загружены";
        readonly public string uiCodeLoadigStatusNotLoeadedText = "Коды НЕ загружены";

        // Кнопка режимы работы и состояние режима работы
        readonly public string uiCodeSendingButtonOnText = "Остановить маркировку";
        readonly public string uiCodeSendingButtonOffText = "Запустить маркировку";
        readonly public string uiCodeSendingStatusOnText = "Маркировка активна";
        readonly public string uiCodeSendingStatusOffText = "Маркировка не активна";

        // Кнопка и режимы работы симуляции печати
        readonly public string uiPrintSimulationButtonOn = "Включить симуляцию";
        readonly public string uiPrintSimulationButtonOff = "Отключить симуляцию";
        readonly public string uiPrintSimulationStatusOn = "Симуляция активна";
        readonly public string uiPrintSimulationStatusOff = "Симуляция не активна";
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            InitializeUI();

            // Здесь будет загрузка конфигурации

            //Binding connectionButtonText = BindingOperations.GetBinding(ConnectionButton, Button.ContentProperty);
            //Binding loadCodeButtonText = BindingOperations.GetBinding(LoadCodeButton, Button.ContentProperty);
            //Binding codeSendingModeButtonText = BindingOperations.GetBinding(CodeSendingModeButton, Button.ContentProperty);

            //Binding connectionStatusTextblockText = BindingOperations.GetBinding(ConnectionStatusText, TextBlock.TextProperty);
            //Binding codeLoadedTextblockText = BindingOperations.GetBinding(CodeLoadedText, TextBlock.TextProperty);
            //Binding codeSendingStatusTextblockText = BindingOperations.GetBinding(CodeSendingStatusText, TextBlock.TextProperty);

            //Binding codesRamainsValue = BindingOperations.GetBinding(CodesRamains, ProgressBar.ValueProperty);
            //Binding codesInQueueValue = BindingOperations.GetBinding(CodesInQueue, ProgressBar.ValueProperty);

            markingCodesQueued = new Queue<string>();
            markingCodePrinted = new Queue<string>();

        }

        private void InitializeUI()
        {
            ConnectionButton.Content = uiConnectButtonText;
            ConnectionStatusText.Text = uiConnectionStatusOfflineText;

            LoadCodeButton.Content = uiCodeLoadingButtonText;
            CodeLoadedText.Text = uiCodeLoadigStatusNotLoeadedText;

            CodeSendingModeButton.Content = uiCodeSendingButtonOffText;
            CodeSendingStatusText.Text = uiCodeSendingStatusOffText;

            

            CodesRamains.Value = 0;
            CodesInQueue.Value = 0;
            CodesInQueue.Maximum = 20;

            Title = "Эскимос.Маркировка";

            EventLog("Инициализировано, готов к работе.");
        }

        public void EventLog(string message)
        {
            TextBox textBox = new TextBox();
            textBox.Width = 365;
            textBox.TextWrapping = TextWrapping.Wrap;
            textBox.IsEnabled = false;
            textBox.Text = DateTime.Now.ToString() + ": " + message;

            EventLogList.Items.Add(textBox);
            EventLogList.ScrollIntoView(EventLogList.Items[EventLogList.Items.Count - 1]);
        }

        private void ConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                Disconnect();
            }
            else
            {
                Connect();
            }
        }

        private void LoadCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (isSelfTestMode)
            {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    markingCodesImported = File.ReadAllText(dialog.FileName).Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
                    markingCodesSource = new Queue<string>(markingCodesImported.Distinct().ToList());
                    isCodeLoaded = true;
                    CodeLoadedText.Text = uiCodeLoadigStatusLoeadedText;
                    CodesRamains.Maximum = markingCodesSource.Count;
                    CodesRamains.Value = markingCodesImported.Count;
                    EventLog("Загружено кодов: " + markingCodesImported.Count + ".");
                    EventLog("Из них уникальных: " + markingCodesSource.Count + ".");
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void CodeSendingModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                EventLog("Не подключен. Подключись к маркиратору"); return;
            }

            if (!isCodeLoaded)
            {
                EventLog("Коды не загружены. Загрузи коды."); return;
            }

            if (isCodeLoadingActive)
            {
                StopCodeLoading();
            }
            else
            {
                StartCodeLoading();
            }

        }

        private void CodesRamains_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void CodesInQueue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void Connect()
        {
            if (isConnected)
            {
                EventLog("Невозможно подключиться. Уже подключен.");
                return;
            }

            if (isSelfTestMode)
            {
                isConnected = true;
                EventLog("Подключение установлено.");

            }
            else
            {
                tcpClient = new TcpClient();
                tcpClient.ConnectAsync(host, port);
                stream = tcpClient.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream);
                EventLog("Подключение установлено.");
            }
            ConnectionButton.Content = uiDisconnectButtonText;
            ConnectionStatusText.Text = uiConnectionStatusOnlineText;
        }

        private void Disconnect()
        {
            if (!isConnected)
            {
                EventLog("Невозможно отключиться. Нет подключения.");
                return;
            }

            if (isSelfTestMode)
            {

            }
            else
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                    reader = null;
                }

                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                    writer = null;
                }

                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                    tcpClient = null;
                }
            }
            isConnected = false;
            ConnectionButton.Content = uiConnectButtonText;
            ConnectionStatusText.Text = uiConnectionStatusOfflineText;
            EventLog("Подключение разорвано.");
        }

        private void SelfTestCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!isSelfTestMode)
            {
                isSelfTestMode = true;
                EventLog("Включен режим самопроверки.");
            }
        }

        private void SelfTestCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isSelfTestMode)
            {
                isSelfTestMode = false;
                EventLog("Отключен режим самопроверки.");
            }
        }

        private void PrintSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPrintSimulationActive)
            {
                StopPrintSimulation();
            }
            else
            {
                StartPrintSimulation();
            }
        }
        private void StopPrintSimulation()
        {
            printSimulationTaskCancellationTokenSource.Cancel();
            printSimulationTaskCancellationTokenSource.Dispose();

            isPrintSimulationActive = false;
            PrintSimulationButton.Content = uiPrintSimulationButtonOff;
            PrintSimulationStatus.Text = uiPrintSimulationStatusOff;

            //Application.Current.Dispatcher.Invoke(() => { EventLog("Симуляция печати приостановлена."); });

        }

        private void StartPrintSimulation()
        {
            printSimulationTaskCancellationTokenSource = new CancellationTokenSource();
            printSimulationTaskCancellationToken = printSimulationTaskCancellationTokenSource.Token;
            printSimulationTask = new Task(PrintSimulation, printSimulationTaskCancellationToken);
            printSimulationTask.Start();
            isPrintSimulationActive = true;
            PrintSimulationButton.Content = uiPrintSimulationButtonOn;
            PrintSimulationStatus.Text = uiPrintSimulationStatusOn;
        }

        private async void PrintSimulation()
        {
            Application.Current.Dispatcher.Invoke(() => { EventLog("Процесс симуляции печати запущен."); });

            while (!printSimulationTaskCancellationToken.IsCancellationRequested)
            {

                string currentMark = markingCodesQueued.Dequeue();
                Application.Current.Dispatcher.Invoke(() => { CodesInQueueUpdate(markingCodesQueued.Count); });
                markingCodePrinted.Enqueue(currentMark);
                Application.Current.Dispatcher.Invoke(() => { EventLog("Печать кода №:" + Environment.NewLine + currentMark); });
                await Task.Delay(1000);
            }

            if (printSimulationTaskCancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() => { EventLog("Процесс симуляции печати прерван."); });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { EventLog("Процесс симуляции печати завершен."); });
            }
        }

        private void StopCodeLoading()
        {
            codeLoadingTaskCancellationTokenSource.Cancel();
            codeLoadingTaskCancellationTokenSource.Dispose();

            isCodeLoadingActive = false;
            CodeSendingModeButton.Content = uiCodeSendingButtonOffText;
            CodeSendingStatusText.Text = uiCodeSendingStatusOffText;
        }

        private void StartCodeLoading()
        {
            codeLoadingTaskCancellationTokenSource = new CancellationTokenSource();
            codeLoadingTaskCancellationToken = codeLoadingTaskCancellationTokenSource.Token;
            codeLoadingTask = new Task(CodeLoading, codeLoadingTaskCancellationToken);
            codeLoadingTask.Start();
            isCodeLoadingActive = true;
            CodeSendingModeButton.Content = uiCodeSendingButtonOnText;
            CodeSendingStatusText.Text = uiCodeSendingStatusOnText;
        }

        private async void CodeLoading()
        {
            Application.Current.Dispatcher.Invoke(() => { EventLog("Процесс загрузки кодов в очередь запущен."); });
            while (!codeLoadingTaskCancellationToken.IsCancellationRequested)
            {
                if (markingCodesQueued.Count <= 5)
                {
                    isCodeLoadingActive = true;
                }
                else if (markingCodesQueued.Count >= 20)
                {
                    isCodeLoadingActive = false;
                }

                if (isCodeLoadingActive)
                {
                    string currentMark = markingCodesSource.Dequeue();
                    Application.Current.Dispatcher.Invoke(() => CodesRamainsUpdate(markingCodesSource.Count));
                    markingCodesQueued.Enqueue(currentMark);
                    Application.Current.Dispatcher.Invoke(() => CodesInQueueUpdate(markingCodesQueued.Count));


                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        EventLog("Загрузка кода №:" + Environment.NewLine +
                        currentMark);
                    });
                }

                await Task.Delay(100);
            }

            if (codeLoadingTaskCancellationToken.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() => { EventLog("Загрузка кодов в очередь прервана."); });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { EventLog("Загрузка кодов в очередь завершена."); });
            }

        }

        public void CodesRamainsUpdate(int value)
        {
            if (value >= 0)
            {
                CodesRamains.Value = value;
            }
            else
            {
                EventLog("Значение счетчика остатка кодов не может быть меньше нуля: " + value);
            }
        }

        public void CodesInQueueUpdate(int value)
        {
            if (value >= 0 && value <= 20)
            {
                CodesInQueue.Value = value;
            }
            else
            {
                EventLog("Значение для счетчика очереди вышло за предели 0-20: " + value);
            }
        }
    }
}
