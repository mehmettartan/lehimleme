using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using JBC_QSCConnect;
using JBCStationControllerSrvServiceReference;

namespace lehimleme
{
    public partial class Form1 : Form
    {
        private BindingSource bindingSource;
        private int stepNumCounter = 1;
        private bool isGridInitialized = false;
        private string currentModelFilePath;

        private JBC_API_QSCConnect qscConnect;
        private System.Windows.Forms.Timer backgroundTimer;

        // Min ve Max değerlerini tutan sözlük
        private Dictionary<string, (double Min, double Max)> columnValueRanges = new Dictionary<string, (double Min, double Max)>
        {
            { "xPozisyonDataGridViewTextBoxColumn", (0, 1000) },
            { "yPozisyon1DataGridViewTextBoxColumn", (0, 500) },
            { "zPozisiyonDataGridViewTextBoxColumn", (0, 100) },
            { "wPozisiyonDataGridViewTextBoxColumn", (0, 100) },
            { "lehimOncesiBeklemeDataGridViewTextBoxColumn", (0, 100) },
            { "lehimSonrasiBeklemeDataGridViewTextBoxColumn", (0, 100) },
            { "lehimMiktarDataGridViewTextBoxColumn", (0, 100) },
            { "lehimHizDataGridViewTextBoxColumn", (0, 100) },
            { "ondenLehimMiktarıDataGridViewTextBoxColumn", (0, 100) },
            { "komponentYuksekligiDataGridViewTextBoxColumn", (0, 100) }
        };
        private Setup copiedRow;
        bool flagFikstür;
        bool saveFail;

        string stationIdentifier = "3030304241424230434638332027315940323136"; // İstasyon kimliği
        string userName = "0"; // Kullanıcı adını buraya ekleyin.
        string temperatureUnit = "C"; // Sıcaklık birimi ('C' veya 'F').

        public Form1()
        {
            InitializeComponent();

            // Olayları ekle
            dataGridView1.RowPrePaint += dataGridView1_RowPrePaint;
            dataGridView1.RowsAdded += dataGridView1_RowsAdded;

            // Her TextBox'ın TextChanged olayına CheckTextBoxesFilled metodunu bağlayın
            txtBxPosX2.TextChanged += TextBox_TextChanged;
            txtBxPosY2.TextChanged += TextBox_TextChanged;
            txtBxPosX3.TextChanged += TextBox_TextChanged;
            txtBxPosY3.TextChanged += TextBox_TextChanged;
            txtBxCoklaX.TextChanged += TextBox_TextChanged;
            txtBxCoklaY.TextChanged += TextBox_TextChanged;
            txtBxCoklaStop.TextChanged += TextBox_TextChanged;
            txtBxCoklaStart.TextChanged += TextBox_TextChanged;

            bindingSource = new BindingSource();
            dataGridView1.DataSource = bindingSource;

            currentModelFilePath =/*@"C:\Varsayilan\model.ini";*/ "C:\\Users\\mehmet.tartan.ALPMERKEZ\\Desktop\\MySettingsFolder";

            dataGridView1.AutoGenerateColumns = false;

            // Robot Hareket sütununu ComboBox olarak tanımla
            ConvertColumnToComboBox("Rob_Hareket", new List<string> { "Lehimleme", "Temizleme" });

            // CellClick olayına işleyici ekleyin
            dataGridView1.CellClick += dataGridView1_CellClick;

            // JBC_API_QSCConnect örneğini formun ömrü boyunca kullanmak için burada oluşturuyoruz.
            qscConnect = new JBC_API_QSCConnect();

            // TextBox'ın KeyDown olayını tanımlıyoruz.
            txBxHavyaSicaklik.KeyDown += new KeyEventHandler(txBxHavyaSicaklik_KeyDown);

            // System.Windows.Forms.Timer'ı başlat
            backgroundTimer = new System.Windows.Forms.Timer();
            backgroundTimer.Interval = 100; // 5 saniye aralıklarla çalışacak
            backgroundTimer.Tick += new EventHandler(TemperatureTimerCallback);
            backgroundTimer.Start();


            //LoadIniData();
        }

        private async void TemperatureTimerCallback(object sender, EventArgs e)
        {
            var port = dc_EnumConstJBCdc_Port.NUM_1; // Port numarası

            try
            {
                // Mevcut sıcaklık değerini oku
                var portStatus = await qscConnect.GetPortStatus_HA(stationIdentifier, port);

                // Sıcaklık bilgisini al
                var temperature = portStatus.ActualTemp; // dc_getTemperature sınıfından sıcaklık bilgisini al

                // Güncel sıcaklık değerini kullanıcıya göstermek için UI thread üzerinde çalış
                this.Invoke((MethodInvoker)delegate
                {
                    btnTemp.Text = $"{temperature.Celsius}°C"; // Celsius özelliğini kullan
                });
            }
            catch (Exception ex)
            {
                // Hata durumunda UI thread üzerinde kullanıcıya bildir
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show($"Bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });

                // Hata durumunda timer'ı durdurmak istiyorsanız:
                backgroundTimer.Stop();
            }
        }

        // TextBox'lar dolu mu kontrol eden metod
        private void CheckTextBoxesFilled()
        {
            // 8 adet TextBox'ın doluluk durumunu kontrol et
            bool areAllTextBoxesFilled = !string.IsNullOrEmpty(txtBxPosX2.Text) &&
                                         !string.IsNullOrEmpty(txtBxPosX3.Text) &&
                                         !string.IsNullOrEmpty(txtBxPosY2.Text) &&
                                         !string.IsNullOrEmpty(txtBxPosY3.Text) &&
                                         !string.IsNullOrEmpty(txtBxCoklaX.Text) &&
                                         !string.IsNullOrEmpty(txtBxCoklaY.Text) &&
                                         !string.IsNullOrEmpty(txtBxCoklaStop.Text) &&
                                         !string.IsNullOrEmpty(txtBxCoklaStart.Text);

            // Eğer tüm TextBox'lar doluysa btnPosCokla'yı etkinleştir, aksi halde devre dışı bırak
            btnPosCokla.Enabled = areAllTextBoxesFilled;
        }

        // TextChanged olayını işleyen metod
        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            // Her TextBox'da değişiklik olduğunda doluluk durumunu kontrol et
            CheckTextBoxesFilled();
        }

        private void ConvertColumnToComboBox(string columnName, List<string> items)
        {
            try
            {
                // Mevcut sütunu bul
                var existingColumn = dataGridView1.Columns["RobHareketGridViewTextBoxColumn"];

                if (existingColumn != null)
                {
                    // Mevcut sütunu kaldır
                    dataGridView1.Columns.Remove(existingColumn);

                    // Yeni ComboBox sütununu oluştur
                    var comboBoxColumn = new DataGridViewComboBoxColumn
                    {
                        Name = columnName,
                        HeaderText = existingColumn.HeaderText,
                        DataPropertyName = existingColumn.DataPropertyName,
                        DropDownWidth = 160,
                        Width = existingColumn.Width,
                        FlatStyle = FlatStyle.Flat,
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
                    };

                    // Seçenekleri ekle
                    comboBoxColumn.Items.AddRange(items.ToArray());

                    // Yeni ComboBox sütununu eski sütunun yerine ekle
                    dataGridView1.Columns.Insert(existingColumn.Index, comboBoxColumn);

                    // Tüm hücrelerde varsayılan olarak "Lehimleme" yazsın
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        row.Cells[comboBoxColumn.Index].Value = "Lehimleme";
                    }
                }
            }
            catch (Exception ex)
            {
                // Hata mesajını göster
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConvertStepNumColumnToButton()
        {
            try
            {
                // Mevcut sütun adlarını göster
                var columnNames = string.Join(", ", dataGridView1.Columns.Cast<DataGridViewColumn>().Select(c => c.Name));
                //MessageBox.Show($"Mevcut sütun adları: {columnNames}");

                // Mevcut sütunu "stepNumDataGridViewTextBoxColumn" adıyla bul
                var stepNumColumn = dataGridView1.Columns["stepNumDataGridViewTextBoxColumn"];

                if (stepNumColumn != null)
                {
                    // Mevcut sütun "DataGridViewButtonColumn" tipinde değilse, değiştirelim
                    if (stepNumColumn is DataGridViewButtonColumn)
                    {
                        MessageBox.Show("Sütun zaten bir buton sütunu olarak ayarlanmış.");
                        return; // Zaten buton kolonu, bu yüzden çık
                    }

                    // "stepNumDataGridViewTextBoxColumn" kolonunu "DataGridViewButtonColumn" olarak değiştirelim
                    var buttonColumn = new DataGridViewButtonColumn
                    {
                        Name = "stepNumDataGridViewButtonColumn", // Yeni ad
                        HeaderText = "Adım No",
                        DataPropertyName = "Step_Num",
                        Text = "Adım",
                        UseColumnTextForButtonValue = false
                    };

                    // Kolonu DataGridView'den kaldır
                    dataGridView1.Columns.Remove(stepNumColumn);

                    // Yeni buton kolonunu ekle
                    dataGridView1.Columns.Insert(0, buttonColumn); // İlk sütun olarak ekleyin

                    // Sütun eklendiğinde mesaj göster
                    // MessageBox.Show("Sütun buton olarak başarıyla güncellendi.");
                }
                else
                {
                    // Hata mesajı göster
                    //MessageBox.Show("stepNumDataGridViewTextBoxColumn adında bir sütun bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButtonColumnValues()
        {
            try
            {
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    // Her satırda "Step_Num" butonunu güncelle
                    if (row.Cells["stepNumDataGridViewButtonColumn"] is DataGridViewButtonCell buttonCell)
                    {
                        int rowIndex = row.Index + 1; // Satır numarasını 1'den başlat
                        buttonCell.Value = rowIndex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while updating button column values: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadIniData()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderPath = Path.Combine(desktopPath, "MySettingsFolder");
                string iniFilePath = Path.Combine(folderPath, "Settings.ini");

                List<Setup> data = ReadIniFile(iniFilePath);
                bindingSource.DataSource = data;

                // Eğer veri yüklendiyse, sayaç değerini son veri satırındaki Step_Num'a ayarla
                if (data.Count > 0)
                {
                    stepNumCounter = data.Max(s => s.Step_Num) + 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading INI data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Dictionary<string, string> headerTextToPropertyMap = new Dictionary<string, string>
        {
            { "Adım No", "Step_Num" },
            { "Robot Hareket", "Rob_Hareket" },
            { "X Pozisyon", "X_Pozisyon_1" },
            { "Y Pozisyon", "Y_Pozisyon_1" },
            { "W Pozisiyon", "W_Pozisiyon" },
            { "Z Pozisyon", "Z_Pozisiyon" },
            { "Lehim Önce Bekleme", "Lehim_Oncesi_Bekleme" },
            { "Lehim Sonra Bekleme", "Lehim_Sonrasi_Bekleme" },
            { "Lehim Miktar", "Lehim_Miktar" },
            { "Lehim Hız", "Lehim_Hiz" },
            { "Komponent Yüksekliği", "Komponent_Yuksekligi" },
            { "Önden Lehim Miktarı", "Onden_Lehim_Miktarı" }
        };

        public List<Setup> ReadIniFile(string filePath)
        {
            var data = new List<Setup>();
            var lines = File.ReadAllLines(filePath);

            Setup setup = null;
            var properties = typeof(Setup).GetProperties();

            foreach (var line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (setup != null && setup.Step_Num != 0) // Boş Step_Num satırlarını eklemeyin
                    {
                        data.Add(setup);
                    }
                    setup = new Setup();
                }
                else if (!string.IsNullOrWhiteSpace(line) && line.Contains("="))
                {
                    var keyValue = line.Split(new[] { '=' }, 2);
                    var headerText = keyValue[0].Trim();
                    var value = keyValue[1].Trim();

                    if (setup != null && headerTextToPropertyMap.TryGetValue(headerText, out string propertyName))
                    {
                        var property = properties.FirstOrDefault(p => p.Name == propertyName);
                        if (property != null)
                        {
                            if (property.PropertyType == typeof(int))
                            {
                                if (int.TryParse(value, out int intValue))
                                {
                                    property.SetValue(setup, intValue);
                                }
                            }
                            else if (property.PropertyType == typeof(double))
                            {
                                if (double.TryParse(value, out double doubleValue))
                                {
                                    property.SetValue(setup, doubleValue);
                                }
                            }
                            else if (property.PropertyType == typeof(string))
                            {
                                property.SetValue(setup, value);
                            }
                        }

                        // Step_Num için özel kontrol
                        if (headerText == "Step_Num")
                        {
                            if (int.TryParse(value, out int stepNum))
                            {
                                setup.Step_Num = stepNum;
                            }
                            else
                            {
                                // Step_Num değeri okunamıyorsa varsayılan bir değer atayın
                                setup.Step_Num = stepNumCounter++;
                            }
                        }
                    }
                }
            }

            // Son setup nesnesini ekleyin, eğer geçerli bir Step_Num varsa
            if (setup != null && setup.Step_Num != 0)
            {
                data.Add(setup);
            }

            return data;
        }

        private bool SaveIniData(string iniFilePath)
        {
            try
            {
                // Dosya yolunun mevcut olup olmadığını kontrol et
                if (string.IsNullOrEmpty(iniFilePath) || !Directory.Exists(Path.GetDirectoryName(iniFilePath)))
                {
                    MessageBox.Show("Geçerli bir dosya yolu bulunamadı. Lütfen doğru bir yol girin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    saveFail = false;
                    return saveFail;
                }

                var data = bindingSource.DataSource as List<Setup> ?? new List<Setup>();

                using (StreamWriter writer = new StreamWriter(iniFilePath))
                {
                    foreach (var setup in data)
                    {
                        writer.WriteLine("[SectionName]"); // Uygun bölüm adını belirtmelisiniz

                        foreach (var kvp in headerTextToPropertyMap)
                        {
                            string headerText = kvp.Key;
                            string propertyName = kvp.Value;
                            string propertyValue = GetPropertyValue(setup, propertyName);

                            writer.WriteLine($"{headerText}={propertyValue}");
                        }
                    }

                    // TextBox değerlerini de yaz
                    writer.WriteLine("[TextBoxValues]"); // TextBox değerleri için bir bölüm ekleyin
                    writer.WriteLine($"Lehim Geri Miktar={txBxLehGeriMiktar.Text}"); // txtBxTextBox1 değerini yazın
                    writer.WriteLine($"Lehim Temizleme Miktar={txBxLehTemizMiktar.Text}"); // txtBxTextBox2 değerini yazın
                    writer.WriteLine($"Havya Sıcaklık={txBxHavyaSicaklik.Text}°C"); // txtBxTextBox3 değerini yazın
                    writer.WriteLine($"Temizleme Süresi={txBxHavyaTemizSure.Text}"); // txtBxTextBox4 değerini yazın
                }

                return saveFail = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving INI data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return saveFail = false;
            }
        }
        // Setup nesnesinin özelliğine, başlık adı ile erişen yardımcı metot
        private string GetPropertyValue(Setup setup, string propertyName)
        {
            var property = typeof(Setup).GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(setup);
                return value != null ? value.ToString() : string.Empty;
            }
            return string.Empty;
        }

        private void addDataToGridView(DataGridView dataGridView, BindingSource bindingSource, int numberOfRowsToAdd, int insertIndex = -1)
        {
            try
            {
                ConvertStepNumColumnToButton();

                // Mevcut veri listesini al
                var currentData = bindingSource.DataSource as List<Setup> ?? new List<Setup>();

                // Yeni veri ekle
                for (int i = 0; i < numberOfRowsToAdd; i++)
                {
                    Setup newSetup = new Setup
                    {
                        Step_Num = stepNumCounter, // Sayaç değerini ata
                        Rob_Hareket = "Lehimleme" // Varsayılan değeri ata
                    };
                    stepNumCounter++; // Sayaç değerini artır

                    if (insertIndex != -1 && insertIndex <= currentData.Count)
                    {
                        currentData.Insert(insertIndex, newSetup);
                        insertIndex++;
                    }
                    else
                    {
                        currentData.Add(newSetup);
                    }
                }

                // Step_Num değerlerini güncelle
                for (int i = 0; i < currentData.Count; i++)
                {
                    currentData[i].Step_Num = i + 1;
                }

                // BindingSource'u güncellemeden önce mevcut verileri ekleyin
                bindingSource.DataSource = null; // Bağlantıyı sıfırla
                bindingSource.DataSource = currentData; // Mevcut verileri yeniden ata

                // DataGridView'in verilerini güncelle
                dataGridView.Refresh();

                UpdateRowColors(dataGridView);

                // Buton değerlerini güncelle
                UpdateButtonColumnValues();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while adding data to the GridView: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateRowColors(DataGridView dataGridView)
        {
            try
            {
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    if (row.Index % 2 == 0) // Çift numaralı satırlar (0 bazlı indeks)
                    {
                        row.DefaultCellStyle.BackColor = Color.White;
                    }
                    else // Tek numaralı satırlar
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while updating row colors: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDataToGridView(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("INI dosyası bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Dosyayı oku ve DataGridView'e yükle
                List<Setup> data = ReadIniFile(filePath);
                if (data == null || data.Count == 0)
                {
                    MessageBox.Show("INI dosyasından veri okunamadı veya dosya boş.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bindingSource.DataSource = data;

                // Eğer veri yüklendiyse, sayaç değerini son veri satırındaki Step_Num'a ayarla
                if (data.Count > 0)
                {
                    stepNumCounter = data.Max(s => s.Step_Num) + 1;
                }
                else
                {
                    stepNumCounter = 1; // Eğer veri yoksa, stepNumCounter'ı 1 olarak başlatın
                }

                // DataGridView'in sütun isimlerini formda belirlenen header text ile koru
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    if (headerTextToPropertyMap.TryGetValue(column.HeaderText, out string propertyName))
                    {
                        column.DataPropertyName = propertyName;
                    }
                }

                // BindingSource ve DataGridView'i güncelle
                bindingSource.ResetBindings(false);
                dataGridView1.Refresh();

                // Buton değerlerini güncelle
                UpdateButtonColumnValues();

                // TextBox değerlerini güncelle
                UpdateTextBoxValues(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading data to the GridView: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void UpdateTextBoxValues(string filePath)
        {
            // INI dosyasını oku
            var iniData = File.ReadAllLines(filePath);

            foreach (var line in iniData)
            {
                if (line.StartsWith("Lehim Geri Miktar="))
                    txBxLehGeriMiktar.Text = line.Substring("Lehim Geri Miktar=".Length);
                else if (line.StartsWith("Lehim Temizleme Miktar="))
                    txBxLehTemizMiktar.Text = line.Substring("Lehim Temizleme Miktar=".Length);
                else if (line.StartsWith("Havya Sıcaklık="))
                    txBxHavyaSicaklik.Text = line.Substring("Havya Sıcaklık=".Length);
                else if (line.StartsWith("Temizleme Süresi="))
                    txBxHavyaTemizSure.Text = line.Substring("Temizleme Süresi=".Length);
            }
        }

        private void btnCutGridRow_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Seçili satırları kontrol et
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    // Veri kaynağını (BindingSource) güncelle
                    var currentData = bindingSource.DataSource as List<Setup>;

                    if (currentData != null)
                    {
                        // Seçili satırlardan indeksleri al ve sıralı bir liste oluştur
                        var selectedIndices = dataGridView1.SelectedRows
                            .Cast<DataGridViewRow>()
                            .Select(row => row.Index)
                            .OrderByDescending(index => index) // Büyükten küçüğe sıralama
                            .ToList();

                        // Seçili satırları veri kaynağından kaldır
                        foreach (var index in selectedIndices)
                        {
                            if (index >= 0 && index < currentData.Count)
                            {
                                currentData.RemoveAt(index);
                            }
                        }

                        // BindingSource'u güncelle
                        bindingSource.DataSource = null; // Bağlantıyı sıfırla
                        bindingSource.DataSource = currentData; // Güncellenmiş verileri yeniden ata

                        // DataGridView'i güncelle
                        dataGridView1.Refresh();

                        // Step_Num değerlerini yeniden numaralandır
                        RenumberRows();
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen silmek için bir veya daha fazla satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır silinirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void RenumberRows()
        {
            // Veri kaynağını al
            var currentData = bindingSource.DataSource as List<Setup>;

            if (currentData != null)
            {
                // Satır numaralarını yeniden düzenle
                for (int i = 0; i < currentData.Count; i++)
                {
                    currentData[i].Step_Num = i + 1; // Step_Num değerini yeniden ayarla
                }

                // BindingSource'u ve DataGridView'i güncelle
                bindingSource.ResetBindings(false);
                dataGridView1.Refresh();
            }
        }

        private void btnAddGridRow_CheckedChanged(object sender, EventArgs e)
        {
            var button = sender as DevExpress.XtraEditors.CheckButton;

            if (button.Checked)
            {
                // Buton aktif (checked) durumunda
                button.BackColor = Color.FromArgb(224,224,244); // Aktif durumda arka plan rengini değiştir
                                                     // Diğer işlemleri burada yapabilirsiniz
            }
            else
            {
                // Buton pasif (unchecked) durumunda
                button.BackColor = Color.FromArgb(224, 224, 244); // Pasif durumda arka plan rengini değiştir
                                               // Diğer işlemleri burada yapabilirsiniz
            }
            try
            {
                // Seçili satırı kontrol et
                int selectedIndex = dataGridView1.SelectedRows.Count > 0 ? dataGridView1.SelectedRows[0].Index : -1;

                // Veri kaynağını al
                var dataSource = bindingSource.DataSource as IList<Setup>;

                if (selectedIndex != -1)
                {
                    // Seçili satırın altına yeni satırı ekle
                    addDataToGridView(dataGridView1, bindingSource, 1, selectedIndex + 1);
                }
                else
                {
                    // Seçili satır yoksa en alta ekle
                    addDataToGridView(dataGridView1, bindingSource, 1);
                }

                // DataGridView'i güncelle
                bindingSource.ResetBindings(false);

                // Satır numaralarını güncelle
                UpdateButtonColumnValues();

                dataGridView1.ClearSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır eklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnPasteGridRow_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Eğer kopyalanmış bir satır varsa
                if (copiedRow != null)
                {
                    // DataGridView'de seçili satır olup olmadığını kontrol edin
                    if (dataGridView1.SelectedRows.Count == 1)
                    {
                        // Seçili satırın indeksi
                        int selectedIndex = dataGridView1.SelectedRows[0].Index;

                        // Veri kaynağını (BindingSource) güncelle
                        var currentData = bindingSource.DataSource as List<Setup>;

                        if (currentData != null && selectedIndex >= 0 && selectedIndex < currentData.Count)
                        {
                            // Seçili satırdaki tüm değerleri kopyalanmış değerlerle değiştir
                            currentData[selectedIndex].Rob_Hareket = copiedRow.Rob_Hareket;
                            currentData[selectedIndex].X_Pozisyon_1 = copiedRow.X_Pozisyon_1;
                            currentData[selectedIndex].Y_Pozisyon_1 = copiedRow.Y_Pozisyon_1;
                            currentData[selectedIndex].W_Pozisiyon = copiedRow.W_Pozisiyon;
                            currentData[selectedIndex].Z_Pozisiyon = copiedRow.Z_Pozisiyon;
                            currentData[selectedIndex].Lehim_Oncesi_Bekleme = copiedRow.Lehim_Oncesi_Bekleme;
                            currentData[selectedIndex].Lehim_Sonrasi_Bekleme = copiedRow.Lehim_Sonrasi_Bekleme;
                            currentData[selectedIndex].Lehim_Miktar = copiedRow.Lehim_Miktar;
                            currentData[selectedIndex].Lehim_Hiz = copiedRow.Lehim_Hiz;
                            currentData[selectedIndex].Komponent_Yuksekligi = copiedRow.Komponent_Yuksekligi;
                            currentData[selectedIndex].Onden_Lehim_Miktarı = copiedRow.Onden_Lehim_Miktarı;

                            // BindingSource'u ve DataGridView'i güncelle
                            bindingSource.ResetBindings(false);
                            dataGridView1.Refresh();

                            MessageBox.Show("Veriler yapıştırıldı.");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Lütfen yapıştırmak için 1 satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Kopyalanmış veri yok.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yapıştırılırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Butona tıklanıp tıklanmadığını kontrol et
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && dataGridView1.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
            {
                // Butona tıklandığında ilgili satırı seç
                dataGridView1.ClearSelection(); // Önceki seçimleri temizle
                dataGridView1.Rows[e.RowIndex].Selected = true;
            }
        }

        private void btnCopyGridRow_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // DataGridView'de seçili satır olup olmadığını kontrol edin
                if (dataGridView1.SelectedRows.Count == 1)
                {
                    // Seçili satırın indeksi
                    int selectedIndex = dataGridView1.SelectedRows[0].Index;

                    // Veri kaynağını (BindingSource) güncelle
                    var currentData = bindingSource.DataSource as List<Setup>;

                    if (currentData != null && selectedIndex >= 0 && selectedIndex < currentData.Count)
                    {
                        // Seçili satırın tüm değerlerini kopyala
                        copiedRow = new Setup
                        {
                            Step_Num = currentData[selectedIndex].Step_Num,
                            Rob_Hareket = currentData[selectedIndex].Rob_Hareket,
                            X_Pozisyon_1 = currentData[selectedIndex].X_Pozisyon_1,
                            Y_Pozisyon_1 = currentData[selectedIndex].Y_Pozisyon_1,
                            W_Pozisiyon = currentData[selectedIndex].W_Pozisiyon,
                            Z_Pozisiyon = currentData[selectedIndex].Z_Pozisiyon,
                            Lehim_Oncesi_Bekleme = currentData[selectedIndex].Lehim_Oncesi_Bekleme,
                            Lehim_Sonrasi_Bekleme = currentData[selectedIndex].Lehim_Sonrasi_Bekleme,
                            Lehim_Miktar = currentData[selectedIndex].Lehim_Miktar,
                            Lehim_Hiz = currentData[selectedIndex].Lehim_Hiz,
                            Komponent_Yuksekligi = currentData[selectedIndex].Komponent_Yuksekligi,
                            Onden_Lehim_Miktarı = currentData[selectedIndex].Onden_Lehim_Miktarı
                        };

                        MessageBox.Show("Satır kopyalandı.");
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen kopyalamak için 1 satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır kopyalanırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnPosCokla_Click(object sender, EventArgs e)
        {
            try
            {
                // Kullanıcıdan alınan değerleri oku
                int start = int.Parse(txtBxCoklaStart.Text); // Başlangıç indeksi (1 tabanlı)
                int stop = int.Parse(txtBxCoklaStop.Text);   // Bitiş indeksi (1 tabanlı)
                int coklaX = int.Parse(txtBxCoklaX.Text);    // Kaç kez çoğaltılacağı
                int coklaY = int.Parse(txtBxCoklaY.Text);    // Y eksenindeki çoğaltma miktarı

                int xpos2 = int.Parse(txtBxPosX2.Text); // X pozisyonu 2
                int xpos3 = int.Parse(txtBxPosX3.Text); // X pozisyonu 3
                int ypos2 = int.Parse(txtBxPosY2.Text); // Y pozisyonu 2
                int ypos3 = int.Parse(txtBxPosY3.Text);

                // Veri kaynağını al
                var dataSource = bindingSource.DataSource as IList<Setup>;
                var newRows = new List<Setup>();

                if (dataSource != null)
                {
                    // Mevcut satır sayısını al
                    int mevcutSatirSayisi = dataSource.Count;

                    bool hasInvalidValue = false;

                    for (int j = 1; j <= coklaY; j++)
                    {
                        // Çoğaltma işlemi
                        for (int k = 1; k <= coklaX; k++) // İlk çoğaltma işleminden sonra k adet ekleme yapılacak
                        {
                            for (int i = start - 1; i < stop; i++) // start ve stop aralığındaki satırları gez
                            {
                                // Mevcut satırdan verileri kopyala ve yeni bir satır oluştur
                                var newRow = new Setup
                                {
                                    Step_Num = mevcutSatirSayisi + 1, // Yeni satır için Step_Num'u ayarla
                                    Rob_Hareket = dataSource[i].Rob_Hareket,
                                    X_Pozisyon_1 = dataSource[i].X_Pozisyon_1,
                                    Y_Pozisyon_1 = dataSource[i].Y_Pozisyon_1,
                                    W_Pozisiyon = dataSource[i].W_Pozisiyon,
                                    Z_Pozisiyon = dataSource[i].Z_Pozisiyon,
                                    Lehim_Oncesi_Bekleme = dataSource[i].Lehim_Oncesi_Bekleme,
                                    Lehim_Sonrasi_Bekleme = dataSource[i].Lehim_Sonrasi_Bekleme,
                                    Lehim_Miktar = dataSource[i].Lehim_Miktar,
                                    Lehim_Hiz = dataSource[i].Lehim_Hiz,
                                    Komponent_Yuksekligi = dataSource[i].Komponent_Yuksekligi,
                                    Onden_Lehim_Miktarı = dataSource[i].Onden_Lehim_Miktarı
                                };

                                if ((k == 1 && j == 1))
                                {
                                    break;
                                }
                                else
                                {
                                    int xpos1 = int.Parse(newRow.X_Pozisyon_1);
                                    int ypos1 = int.Parse(newRow.Y_Pozisyon_1);

                                    int newXpos = xpos1 + (xpos2 - int.Parse(dataSource[start - 1].X_Pozisyon_1)) * (k - 1) + (xpos3 - int.Parse(dataSource[start - 1].X_Pozisyon_1)) * (j - 1);
                                    int newYpos = ypos1 + (ypos2 - int.Parse(dataSource[start - 1].Y_Pozisyon_1)) * (k - 1) + (ypos3 - int.Parse(dataSource[start - 1].Y_Pozisyon_1)) * (j - 1);

                                    bool isXValid = true;
                                    bool isYValid = true;

                                    // Min ve max değer kontrolü yap
                                    if (columnValueRanges.TryGetValue("xPozisyonDataGridViewTextBoxColumn", out var xRange))
                                    {
                                        if (newXpos < xRange.Min || newXpos > xRange.Max)
                                        {
                                            isXValid = false;
                                        }
                                    }
                                    if (columnValueRanges.TryGetValue("yPozisyon1DataGridViewTextBoxColumn", out var yRange))
                                    {
                                        if (newYpos < yRange.Min || newYpos > yRange.Max)
                                        {
                                            isYValid = false;
                                        }
                                    }

                                    // Min ve max değer aralığında olup olmadığını kontrol et
                                    if (!isXValid || !isYValid)
                                    {
                                        hasInvalidValue = true;
                                        break; // Döngüden çık
                                    }

                                    // Yeni satırı geçici listeye ekle
                                    newRow.X_Pozisyon_1 = newXpos.ToString();
                                    newRow.Y_Pozisyon_1 = newYpos.ToString();
                                    newRows.Add(newRow);

                                    mevcutSatirSayisi++; // Yeni satır eklendiğinde mevcut satır sayısını güncelle
                                }
                            }

                            if (hasInvalidValue)
                            {
                                break; // Satırların geçerliliği sağlanamadıysa dış döngüden çık
                            }
                        }

                        if (hasInvalidValue)
                        {
                            break; // Satırların geçerliliği sağlanamadıysa dış döngüden çık
                        }
                    }

                    // Geçerli satırlar varsa veri kaynağına ekle
                    if (!hasInvalidValue)
                    {
                        foreach (var row in newRows)
                        {
                            dataSource.Add(row);
                        }

                        // DataGridView'i güncelle
                        bindingSource.ResetBindings(false);

                        // Satır numaralarını güncelle
                        UpdateButtonColumnValues();
                    }
                    else
                    {
                        MessageBox.Show("Geçersiz değerler bulundu. Satırlar oluşturulmadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Veri kaynağı boş veya geçersiz.");
                }
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"Giriş değerleri geçersiz: {ex.Message}", "Format Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                MessageBox.Show($"Dışında bir indeks hatası oluştu: {ex.Message}", "İndeks Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnYeniMdl_Click(object sender, EventArgs e)
        {
            // DataGridView'i temizle
            bindingSource.DataSource = null;
            txBxLehGeriMiktar.Text = null;
            txBxLehTemizMiktar.Text = null;
            txBxHavyaSicaklik.Text = null;
            txBxHavyaTemizSure.Text = null;
            txBxModelName.Text = null;
            txtBxCoklaStart.Text = null;
            txtBxCoklaStop.Text = null;
            txtBxCoklaX.Text = null;
            txtBxCoklaY.Text = null;
            txtBxPosX2.Text = null;
            txtBxPosY2.Text = null;
            txtBxPosX3.Text = null;
            txtBxPosY3.Text = null;
        }

        private void btnModelSec_Click(object sender, EventArgs e)
        {
            try
            {
                // Klasör seçici aç
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string selectedFolderPath = folderDialog.SelectedPath;

                        // Model dosyalarını listele
                        var modelFiles = Directory.GetFiles(selectedFolderPath, "*.ini");

                        if (modelFiles.Length == 0)
                        {
                            MessageBox.Show("Model dosyası bulunamadı.");
                            return;
                        }

                        // Kullanıcıdan bir dosya seçmesini iste
                        using (OpenFileDialog fileDialog = new OpenFileDialog())
                        {
                            fileDialog.InitialDirectory = selectedFolderPath;
                            fileDialog.Filter = "INI Files (*.ini)|*.ini";
                            fileDialog.Title = "Bir model dosyası seçin";

                            if (fileDialog.ShowDialog() == DialogResult.OK)
                            {
                                currentModelFilePath = fileDialog.FileName; // Dosya yolunu atayın

                                // Seçilen dosyayı DataGridView'e yükle
                                LoadDataToGridView(currentModelFilePath);

                                // Model adını modetebx'e yazdır
                                txBxModelName.Text = Path.GetFileNameWithoutExtension(currentModelFilePath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnMdlKaydet_Click(object sender, EventArgs e)
        {
            try
            {
                // 'Rob_Hareket' sütununda "Lehimleme" seçili olan satırları kontrol et
                bool allRowsValid = true;

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    // Satırdaki 'Rob_Hareket' hücresinin değerini kontrol et
                    var robHareketValue = row.Cells["Rob_Hareket"].Value?.ToString();

                    if (robHareketValue == "Lehimleme")
                    {
                        bool allCellsFilled = true;

                        // Satırdaki her hücreyi kontrol et
                        foreach (DataGridViewCell cell in row.Cells)
                        {
                            // Hücre değerini boşluk için kontrol et
                            if (cell.Value == null || string.IsNullOrWhiteSpace(cell.Value.ToString()))
                            {
                                allCellsFilled = false;
                                break;
                            }
                        }

                        if (!allCellsFilled)
                        {
                            allRowsValid = false;
                            MessageBox.Show("Tüm hücrelerin doldurulmuş olduğundan emin olun. 'Lehimleme' seçili iken tüm satırların hücreleri boş olmamalıdır.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            break;
                        }
                    }
                }

                if (allRowsValid)
                {
                    // TextBox'ların boş olup olmadığını kontrol et
                    if (string.IsNullOrWhiteSpace(txBxLehGeriMiktar.Text) ||
                        string.IsNullOrWhiteSpace(txBxLehTemizMiktar.Text) ||
                        string.IsNullOrWhiteSpace(txBxHavyaSicaklik.Text) ||
                        string.IsNullOrWhiteSpace(txBxHavyaTemizSure.Text))
                    {
                        MessageBox.Show("Lütfen tüm gerekli bilgileri girin.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string newModelName = txBxModelName.Text.Trim();

                    if (string.IsNullOrEmpty(newModelName))
                    {
                        MessageBox.Show("Lütfen bir model adı girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Yeni dosya yolu için hedef klasörü belirle
                    string targetDirectory = @"C:\Users\mehmet.tartan.ALPMERKEZ\Desktop\MySettingsFolder";

                    // Hedef klasörün var olup olmadığını kontrol et, yoksa oluştur
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    // Yeni model adıyla dosya yolunu güncelle
                    string newFilePath = Path.Combine(targetDirectory, newModelName + ".ini");

                    // Dosya adını ve yolunu güncelle
                    currentModelFilePath = newFilePath;

                    // SaveIniData fonksiyonuna model adını gönder
                    bool save = SaveIniData(currentModelFilePath);
                    if (save)
                    {
                        MessageBox.Show("Veriler başarıyla kaydedildi.");
                    }
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Dosya hatası: {ex.Message}", "Dosya Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Erişim hatası: {ex.Message}", "Erişim Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            string columnName = dataGridView1.Columns[e.ColumnIndex].Name;

            // Sütunun başlık ismini al
            string columnHeaderText = dataGridView1.Columns[e.ColumnIndex].HeaderText;



            if (columnValueRanges.ContainsKey(columnName))
            {
                if (double.TryParse(e.FormattedValue.ToString(), out double newValue))
                {
                    var (Min, Max) = columnValueRanges[columnName];
                    if (newValue < Min || newValue > Max)
                    {
                        e.Cancel = true;
                        MessageBox.Show($"{columnHeaderText} sütununa girilen değer {Min} ile {Max} arasında olmalıdır.", "Geçersiz Değer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    //e.Cancel = true;
                    //MessageBox.Show("Geçerli bir sayı girin.", "Geçersiz Giriş", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // Sütun adı kontrolü
            var robHareketColumn = dataGridView1.Columns["Rob_Hareket"];
            var xPozisyonColumn = dataGridView1.Columns["xPozisyonDataGridViewTextBoxColumn"]; // Devre dışı bırakılacak sütun


            if (robHareketColumn != null && e.ColumnIndex == robHareketColumn.Index)
            {
                // Satır ve hücre nesnesinin varlığını kontrol et
                if (e.RowIndex >= 0 && e.RowIndex < dataGridView1.Rows.Count)
                {
                    var row = dataGridView1.Rows[e.RowIndex];
                    if (row != null && row.Cells["Rob_Hareket"] != null)
                    {
                        // Satırın 'Rob_Hareket' hücresinin değerini al
                        var robHareketValue = row.Cells["Rob_Hareket"].Value?.ToString();

                        // Eğer değer "Temizleme" ise, belirli sütunları devre dışı bırak
                        if (robHareketValue == "Temizleme")
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                // X_Pozisyon_1 sütunundan itibaren tüm hücreleri devre dışı bırak
                                if (cell.OwningColumn.Index >= xPozisyonColumn.Index)
                                {
                                    cell.ReadOnly = true;
                                    cell.Style.BackColor = Color.DarkGray; // Hücrenin arka plan rengini değiştirir
                                }
                            }
                        }
                        else if (robHareketValue == "Lehimleme")
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                // X_Pozisyon_1 sütunundan itibaren tüm hücreleri etkinleştir
                                if (cell.OwningColumn.Index >= xPozisyonColumn.Index)
                                {
                                    cell.ReadOnly = false;
                                    cell.Style.BackColor = Color.White; // Hücrenin arka plan rengini geri alır
                                }
                            }
                        }
                    }
                }
            }
        }

        private void dataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            // Yeni eklenen satırların başlangıç indeksini belirle
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                foreach (DataGridViewCell cell in dataGridView1.Rows[i].Cells)
                {
                    if (i % 2 == 0)
                    {
                        // Çift indeksli satırlar için gri renk
                        cell.Style.BackColor = Color.LightGray;
                    }
                    else
                    {
                        // Tek indeksli satırlar için beyaz renk
                        cell.Style.BackColor = Color.White;
                    }
                }

                var row = dataGridView1.Rows[i];

                // 'Rob_Hareket' sütununun hücresinin değerini kontrol et
                if (row.Cells["Rob_Hareket"].Value?.ToString() == "Temizleme")
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        var xPozisyonColumn = dataGridView1.Columns["xPozisyonDataGridViewTextBoxColumn"];
                        if (xPozisyonColumn != null && cell.OwningColumn.Index >= xPozisyonColumn.Index)
                        {
                            cell.ReadOnly = true;
                            cell.Style.BackColor = Color.LightGray; // Hücrenin arka plan rengini değiştirir
                        }
                    }
                }
                else if (row.Cells["Rob_Hareket"].Value?.ToString() == "Lehimleme")
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        var xPozisyonColumn = dataGridView1.Columns["xPozisyonDataGridViewTextBoxColumn"];
                        if (xPozisyonColumn != null && cell.OwningColumn.Index >= xPozisyonColumn.Index)
                        {
                            cell.ReadOnly = false;
                            cell.Style.BackColor = Color.White; // Hücrenin arka plan rengini geri alır
                        }
                    }
                }
            }
        }

        private void btnLehimYap_Click(object sender, EventArgs e)
        {
            try
            {
                // Seçili satırları kontrol et
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    // Seçili satırdaki Rob_Hareket sütunundaki değeri al
                    var selectedRow = dataGridView1.SelectedRows[0];
                    string robHareket = selectedRow.Cells["Rob_Hareket"].Value.ToString();

                    // Değeri kontrol et
                    if (robHareket.Equals("Lehimleme", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Seçili satırın robot hareketi: Lehimleme", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // Lehimleme işlemi için yapılacaklar
                    }
                    else if (robHareket.Equals("Temizleme", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Seçili satırın robot hareketi: Temizleme", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // Temizleme işlemi için yapılacaklar
                    }
                    else
                    {
                        MessageBox.Show("Seçili satırın robot hareketi tanımlanamıyor.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen pozisyona gitmek için bir satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır kontrol edilirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnPosGit_Click(object sender, EventArgs e)
        {
            try
            {
                // Seçili satırları kontrol et
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    // Seçili satırdaki Rob_Hareket sütunundaki değeri al
                    var selectedRow = dataGridView1.SelectedRows[0];
                    string robHareket = selectedRow.Cells["Rob_Hareket"].Value.ToString();

                    // Değeri kontrol et
                    if (robHareket.Equals("Lehimleme", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Seçili satırın robot hareketi: Lehimleme", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // Lehimleme işlemi için yapılacaklar
                    }
                    else if (robHareket.Equals("Temizleme", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Seçili satırın robot hareketi: Temizleme", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // Temizleme işlemi için yapılacaklar
                    }
                    else
                    {
                        MessageBox.Show("Seçili satırın robot hareketi tanımlanamıyor.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen pozisyona gitmek için bir satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır kontrol edilirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnPosSave_Click(object sender, EventArgs e)
        {
            try
            {
                try
                {
                    // Seçili satır olup olmadığını kontrol et
                    if (dataGridView1.SelectedRows.Count > 0)
                    {
                        // Seçili satırı al
                        var selectedRow = dataGridView1.SelectedRows[0];
                        int selectedRowIndex = selectedRow.Index;

                        // Seçili satırın Rob_Hareket sütunundaki değeri al
                        string robHareket = selectedRow.Cells["Rob_Hareket"].Value.ToString();

                        // Eğer Rob_Hareket 'Temizleme' değilse işlemlere devam et
                        if (robHareket != "Temizleme")
                        {
                            // TextBox'lardan gelen değerleri al
                            string xValue = txBxXValue.Text;
                            string yValue = txBxYValue.Text;
                            string wValue = txBxWValue.Text;
                            string zValue = txBxZValue.Text;

                            // Seçili satırın ilgili hücrelerine değerleri yaz
                            selectedRow.Cells["xPozisyonDataGridViewTextBoxColumn"].Value = xValue;
                            selectedRow.Cells["yPozisyon1DataGridViewTextBoxColumn"].Value = yValue;
                            selectedRow.Cells["wPozisiyonDataGridViewTextBoxColumn"].Value = wValue;
                            selectedRow.Cells["zPozisiyonDataGridViewTextBoxColumn"].Value = zValue;

                            // Üstteki satırları aşağıdan yukarıya tarayarak ilk "Lehimleme" satırını bul
                            DataGridViewRow lehimlemeRow = null;

                            for (int i = selectedRowIndex - 1; i >= 0; i--)
                            {
                                var row = dataGridView1.Rows[i];
                                string upperRowRobHareket = row.Cells["Rob_Hareket"].Value.ToString();

                                if (upperRowRobHareket == "Lehimleme")
                                {
                                    lehimlemeRow = row;
                                    break;
                                }
                            }

                            // Eğer "Lehimleme" satırı bulunduysa son 6 kolonun değerlerini al ve seçili satıra yaz
                            if (lehimlemeRow != null)
                            {
                                // Lehimleme satırının son 6 kolonundaki değerleri al
                                var lehimOncesiBekleme = lehimlemeRow.Cells["lehimOncesiBeklemeDataGridViewTextBoxColumn"].Value;
                                var lehimSonrasiBekleme = lehimlemeRow.Cells["lehimSonrasiBeklemeDataGridViewTextBoxColumn"].Value;
                                var lehimMiktar = lehimlemeRow.Cells["lehimMiktarDataGridViewTextBoxColumn"].Value;
                                var lehimHiz = lehimlemeRow.Cells["lehimHizDataGridViewTextBoxColumn"].Value;
                                var ondenLehimMiktarı = lehimlemeRow.Cells["ondenLehimMiktarıDataGridViewTextBoxColumn"].Value;
                                var komponentYuksekligi = lehimlemeRow.Cells["komponentYuksekligiDataGridViewTextBoxColumn"].Value;

                                // Bu değerleri seçili satırın son 6 kolonuna yaz
                                selectedRow.Cells["lehimOncesiBeklemeDataGridViewTextBoxColumn"].Value = lehimOncesiBekleme;
                                selectedRow.Cells["lehimSonrasiBeklemeDataGridViewTextBoxColumn"].Value = lehimSonrasiBekleme;
                                selectedRow.Cells["lehimMiktarDataGridViewTextBoxColumn"].Value = lehimMiktar;
                                selectedRow.Cells["lehimHizDataGridViewTextBoxColumn"].Value = lehimHiz;
                                selectedRow.Cells["ondenLehimMiktarıDataGridViewTextBoxColumn"].Value = ondenLehimMiktarı;
                                selectedRow.Cells["komponentYuksekligiDataGridViewTextBoxColumn"].Value = komponentYuksekligi;

                                // DataGridView'i güncelle
                                dataGridView1.Refresh();
                            }
                            else
                            {
                                MessageBox.Show("Üst satırlarda 'Lehimleme' hareketi bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Temizleme işleminde pozisyon kaydedilemez.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Lütfen pozisyona gitmek için bir satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Satır işlenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Satır seçilirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnHavyaSifirla_Click(object sender, EventArgs e)
        {

        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            // Seçili satır var mı kontrol et
            if (dataGridView1.SelectedRows.Count > 0)
            {
                // Seçili satırı al
                var selectedRow = dataGridView1.SelectedRows[0];

                // Rob_Hareket sütunundaki değeri kontrol et
                string robHareket = selectedRow.Cells["Rob_Hareket"].Value.ToString();

                // Eğer Rob_Hareket "Lehimleme" ise butonları etkinleştir, değilse devre dışı bırak
                if (robHareket == "Lehimleme")
                {
                    bool isXFilled = !string.IsNullOrEmpty(selectedRow.Cells["xPozisyonDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isYFilled = !string.IsNullOrEmpty(selectedRow.Cells["yPozisyon1DataGridViewTextBoxColumn"].Value?.ToString());
                    bool isWFilled = !string.IsNullOrEmpty(selectedRow.Cells["wPozisiyonDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isZFilled = !string.IsNullOrEmpty(selectedRow.Cells["zPozisiyonDataGridViewTextBoxColumn"].Value?.ToString());

                    // Son 6 sütunu kontrol et
                    bool isLehOnceFilled = !string.IsNullOrEmpty(selectedRow.Cells["lehimOncesiBeklemeDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isLLehSonFilled = !string.IsNullOrEmpty(selectedRow.Cells["lehimSonrasiBeklemeDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isLehMiktFilled = !string.IsNullOrEmpty(selectedRow.Cells["lehimMiktarDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isLehHizzFilled = !string.IsNullOrEmpty(selectedRow.Cells["lehimHizDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isOnLehMkFilled = !string.IsNullOrEmpty(selectedRow.Cells["ondenLehimMiktarıDataGridViewTextBoxColumn"].Value?.ToString());
                    bool isKomYuksFilled = !string.IsNullOrEmpty(selectedRow.Cells["komponentYuksekligiDataGridViewTextBoxColumn"].Value?.ToString());




                    // Eğer tüm değerler doluysa btnPosGit'i etkinleştir
                    if (isXFilled && isYFilled && isWFilled && isZFilled)
                    {
                        // Seçilen satır sayısını kontrol et
                        if (dataGridView1.SelectedRows.Count > 1)
                        {
                            // Eğer iki veya daha fazla satır seçiliyse, butonu pasif yap
                            btnPosGit.Enabled = false;
                        }
                        else
                        {
                            // Eğer yalnızca bir satır seçiliyse, butonu aktif yap
                            btnPosGit.Enabled = true;
                        }

                    }
                    else
                    {
                        btnPosGit.Enabled = false;
                    }

                    // Eğer tüm değerler doluysa btnPosGit'i etkinleştir
                    if (isLehOnceFilled && isLLehSonFilled && isLehMiktFilled && isLehHizzFilled && isOnLehMkFilled && isKomYuksFilled)
                    {
                        // Seçilen satır sayısını kontrol et
                        if (dataGridView1.SelectedRows.Count > 1)
                        {
                            // Eğer iki veya daha fazla satır seçiliyse, butonu pasif yap
                            btnLehimYap.Enabled = false;
                        }
                        else
                        {
                            // Eğer yalnızca bir satır seçiliyse, butonu aktif yap
                            btnLehimYap.Enabled = true;
                        }
                    }
                    else
                    {
                        btnLehimYap.Enabled = false;
                    }

                    btnPosSave.Enabled = true;
                }
                else
                {
                    // "Lehimleme" değilse tüm butonları devre dışı bırak
                    btnPosSave.Enabled = false;
                    btnLehimYap.Enabled = false;
                    btnPosGit.Enabled = false;
                }
            }
            else
            {
                // Seçili satır yoksa tüm butonları devre dışı bırak
                btnPosSave.Enabled = false;
                btnLehimYap.Enabled = false;
                btnPosGit.Enabled = false;
            }
        }

        private void dataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            // DataGridView'e tıklandığında seçimi temizle
            dataGridView1.ClearSelection();
        }

        private void dataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            // Her bir hücrenin rengini belirleyin
            foreach (DataGridViewCell cell in dataGridView1.Rows[e.RowIndex].Cells)
            {
                if (e.RowIndex % 2 == 0)
                {
                    // Çift indeksli satırlar için gri renk
                    cell.Style.BackColor = Color.LightGray;
                }
                else
                {
                    // Tek indeksli satırlar için beyaz renk
                    cell.Style.BackColor = Color.White;
                }
            }
        }

        private async void txBxHavyaSicaklik_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Enter tuşunun "ding" sesi çıkarmasını engeller.

                // TextBox içindeki değeri kontrol et.
                if (int.TryParse(txBxHavyaSicaklik.Text, out int newTemperature))
                {
                    // Kullanıcıdan alınan sıcaklık değerini cihazınıza gönder.
                    await SetTemperatureAsync(newTemperature);
                }
                else
                {
                    MessageBox.Show("Geçerli bir sıcaklık değeri girin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Sıcaklık ayarlama işlemini gerçekleştiren metod.
        private async Task SetTemperatureAsync(int newTemperature)
        {


            try
            {
                // İstasyon kontrol modunu ayarla.
                await qscConnect.SetControlMode(
                    stationIdentifier,
                    dc_EnumConstJBCdc_ControlModeConnection.CONTROL,
                    userName);

                // İstasyon sıcaklığını ayarla.
                await qscConnect.SetPortToolSelectedTemp(
                    stationIdentifier,
                    dc_EnumConstJBCdc_Port.NUM_1,
                    newTemperature,
                    temperatureUnit);

                //MessageBox.Show("Sıcaklık başarıyla ayarlandı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bir hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnMin_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void exit_Click(object sender, EventArgs e)
        {
            backgroundTimer.Stop();

            // Tüm formları kapat
            this.Close();

            // Zorla kapanma durumu için Environment.Exit kullanın
            Environment.Exit(0);
        }
    }
}