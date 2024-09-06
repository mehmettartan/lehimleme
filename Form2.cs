using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace lehimleme
{
    public partial class Form2 : Form
    {
        private BindingSource bindingSource;
        private Setup copiedRow;
        private int stepNumCounter = 1;
        public Form2()
        {
            InitializeComponent();
        }

        private void btnAddGridRow_CheckedChanged(object sender, EventArgs e)
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
        }

        private void btnCutGridRow_CheckedChanged(object sender, EventArgs e)
        {
            // Seçili satır olup olmadığını kontrol et
            if (dataGridView1.SelectedRows.Count > 0)
            {
                // Seçili satırı alın
                var selectedRow = dataGridView1.SelectedRows[0];

                // Veri kaynağını (BindingSource) güncelle
                var currentData = bindingSource.DataSource as List<Setup>;

                if (currentData != null)
                {
                    // Seçili satırın indeksini bulun
                    int rowIndex = selectedRow.Index;

                    // Satırı veri kaynağından kaldır
                    currentData.RemoveAt(rowIndex);

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
                MessageBox.Show("Lütfen silmek için bir satır seçin.");
            }
        }

        private void btnCopyGridRow_CheckedChanged(object sender, EventArgs e)
        {
            // DataGridView'de seçili satır olup olmadığını kontrol edin
            if (dataGridView1.SelectedRows.Count > 0)
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
                MessageBox.Show("Lütfen kopyalamak için bir satır seçin.");
            }
        }

        private void btnPasteGridRow_CheckedChanged(object sender, EventArgs e)
        {
            // Eğer kopyalanmış bir satır varsa
            if (copiedRow != null)
            {
                // DataGridView'de seçili satır olup olmadığını kontrol edin
                if (dataGridView1.SelectedRows.Count > 0)
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
                    MessageBox.Show("Lütfen yapıştırmak için bir satır seçin.");
                }
            }
            else
            {
                MessageBox.Show("Kopyalanmış veri yok.");
            }
        }

        private void addDataToGridView(DataGridView dataGridView, BindingSource bindingSource, int numberOfRowsToAdd, int insertIndex = -1)
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

            // Buton değerlerini güncelle
            UpdateButtonColumnValues();
        }

        private void ConvertStepNumColumnToButton()
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

        private void UpdateButtonColumnValues()
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
    }
}
