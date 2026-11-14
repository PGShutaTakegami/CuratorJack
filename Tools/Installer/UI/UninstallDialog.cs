using System.Drawing;
using System.Windows.Forms;

namespace CJInstaller.UI
{
    /// <summary>アンインストールの確認画面。セーブデータも消すかを選べる（初期値は残す）</summary>
    sealed class UninstallDialog : Form
    {
        readonly CheckBox deleteSaveDataCheck = new CheckBox();

        public UninstallDialog(string caption, string directory)
        {
            Text = caption;
            Font = new Font("Yu Gothic UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 200);

            var question = new Label { Text = $"{Product.Name} をアンインストールしますか？", Font = new Font("Yu Gothic UI", 11f, FontStyle.Bold), Location = new Point(16, 14), AutoSize = true };
            var location = new Label { Text = directory, Location = new Point(16, 44), Size = new Size(428, 20), AutoEllipsis = true };
            deleteSaveDataCheck.Text = "セーブデータ（実績・戦績・名前・アイコン・設定）も削除する";
            deleteSaveDataCheck.SetBounds(16, 78, 428, 22);
            var warning = new Label { Text = "削除したセーブデータは元に戻せません。チェックしなければセーブデータは残ります。", ForeColor = Color.Firebrick, Location = new Point(34, 102), Size = new Size(410, 34) };
            var uninstallButton = new Button { Text = "アンインストール", DialogResult = DialogResult.OK };
            uninstallButton.SetBounds(232, 154, 110, 30);
            var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel };
            cancelButton.SetBounds(350, 154, 94, 30);

            Controls.AddRange(new Control[] { question, location, deleteSaveDataCheck, warning, uninstallButton, cancelButton });
            AcceptButton = uninstallButton;
            CancelButton = cancelButton;
        }

        public bool DeleteSaveData => deleteSaveDataCheck.Checked;
    }
}
