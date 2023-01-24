// Jonathan Dean (ke4ukz@gmx.com)
// 2023-01-23

using System.Windows.Forms;
using System.Diagnostics;
using System.Windows.Forms.Design;
using System.Reflection.Metadata;
using System.IO.Compression;
using System.Text.Json;
using System.IO;

namespace CCDInfo {
	public partial class frmMain : Form {
		
		private Stream getDATFromPKG(Stream stream) {
			var ccdZA = new ZipArchive(stream);
			foreach (var za in ccdZA.Entries) {
				var fi = new FileInfo(za.Name);
				if (fi.Extension.ToLower() == ".dat") {
					var datStream = za.Open();
					return datStream;
				}
			}
			return Stream.Null;
		}

		private Stream getPKGFromZIP(Stream stream) {
			var zipZA = new ZipArchive(stream);
			foreach (var zae in zipZA.Entries) {
				if ((new FileInfo(zae.Name)).Extension.ToLower() == ".pkg") {
					// found the CCD
					return zae.Open();
				}
			}
			return Stream.Null;
		}

		private string getJSONFromDAT(Stream stream) {
			var sr = new StreamReader(stream);
			return sr.ReadToEnd();
		}

		private string getJSONStringProperty(JsonElement json, string property) {
			JsonElement d;
			if (json.TryGetProperty(property, out d)) {
				return d.GetString();
			} else {
				return "";
			}
		}

		private void addListItem(string name, string value) {
			var x = lsvData.Items.Add(name);
			x.SubItems.Add(value);
		}

		private void parseJSONData(string data) {
			txtJSONOutput.Text = data;
			JsonElement jdata = JsonSerializer.Deserialize<dynamic>(data);
			JsonElement d, d2, d3;
			lsvData.Items.Clear();
			addListItem("Device Type", getJSONStringProperty(jdata, "deviceType"));
			addListItem("Manufacturer", getJSONStringProperty(jdata, "manufacturer"));
			addListItem("Description", getJSONStringProperty(jdata, "description"));
			addListItem("Base Model", getJSONStringProperty(jdata, "baseModel"));
			addListItem("Driver Version", getJSONStringProperty(jdata, "driverVersion"));
			addListItem("Driver Version Date", getJSONStringProperty(jdata, "driverVersionDate"));
			
			if (jdata.TryGetProperty("supportedModels", out d)) {
				addListItem("Supported Models", String.Join(", ", d.EnumerateArray()));
			}
			if (jdata.TryGetProperty("communication", out d)) {
				d.TryGetProperty("type", out d2);
				addListItem("Communication Type", d2.GetString().ToUpper());
			}
			if (jdata.TryGetProperty("features", out d)) {
				if (d.ValueKind != JsonValueKind.Null) {
					foreach (JsonElement x in d.EnumerateArray()) {
						if (x.TryGetProperty("displayName", out d2)) {
							if (x.TryGetProperty("value", out d3)) {
								addListItem(d2.GetString(), d3.GetBoolean() ? "Yes" : "No");
							}
						}

					}
				}
			}
			if (jdata.TryGetProperty("inputs", out d)) {
				if (d.ValueKind != JsonValueKind.Null) {
					foreach (JsonElement x in d.EnumerateArray()) {
						if (x.TryGetProperty("inputDescription", out d2)) {
							if (x.TryGetProperty("index", out d3)) {
								addListItem(d2.GetString(), $"Input {d3.GetInt16()}");
							}
						}
					}
				}
			}
			if (jdata.TryGetProperty("userAttributes", out d)) {
				int i = 0;
				if (d.ValueKind != JsonValueKind.Null) {
					foreach (JsonElement x in d.EnumerateArray()) {
						i++;
						if (x.TryGetProperty("label", out d2)) {
							if (x.TryGetProperty("description", out d3)) {
								addListItem($"User Attribute {i}", $"{d2.GetString()}: {d3.GetString()}");
							}
						}
					}
				}
			}
		}

		private void openCCDFile(string filename) {
			var fileInfo = new FileInfo(filename);
			if (fileInfo.Extension.ToLower() == ".zip") {
				var zip = new StreamReader(filename).BaseStream;
				var pkg = getPKGFromZIP(zip);
				if (pkg == Stream.Null) {
					MessageBox.Show("No PKG file found in ZIP archive", "Invalid Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					zip.Close();
					return;
				}
				var dat = getDATFromPKG(pkg);
				if (dat == Stream.Null) {
					MessageBox.Show("No DAT file found in PKG archive", "Invalid Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					pkg.Close();
					zip.Close();
					return;
				}
				var json = getJSONFromDAT(dat);
				dat.Close();
				pkg.Close();
				zip.Close();
				parseJSONData(json);
			} else if (fileInfo.Extension.ToLower() == ".pkg") {
				// This is already a CCD
				var dat = getDATFromPKG(new StreamReader(filename).BaseStream);
				if (dat == Stream.Null) {
					MessageBox.Show("No DAT file found in PKG archive", "Invalid Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				var json = getJSONFromDAT(dat);
				dat.Close();
				parseJSONData(json);
			} else if (fileInfo.Extension.ToLower() == ".dat") {
				var dat = new StreamReader(filename).BaseStream;
				var json = getJSONFromDAT(dat);
				dat.Close();
				parseJSONData(json);
			} else {
				MessageBox.Show("Not a ZIP or PKG or DAT file", "Invalid Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

		}

		public frmMain() {
			InitializeComponent();
		}

		private void cmdBrowse_Click(object sender, EventArgs e) {
			var ofd = new OpenFileDialog();
			ofd.Title = "Select CCD File";
			ofd.Filter = "CCD Files|*.pkg;*.zip;*.dat|All Files|*.*";
			ofd.DefaultExt = "pkg";
			ofd.Multiselect = false;
			ofd.CheckFileExists = true;
			var ret = ofd.ShowDialog();
			if (ret != DialogResult.OK) {
				return;
			}
			openCCDFile(ofd.FileName);
		}

		private void frmMain_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effect = DragDropEffects.Copy;
			}
		}

		private void frmMain_DragDrop(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				txtCCDPKG.Text = files[0];
				openCCDFile(txtCCDPKG.Text);
			}
		}

		private void txtCCDPKG_Leave(object sender, EventArgs e) {
			openCCDFile(txtCCDPKG.Text);
		}
	}
}