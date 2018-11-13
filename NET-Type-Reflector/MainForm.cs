﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.CompilerServices;

using System.Reflection;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;

namespace NetTypeReflector
{
    public partial class MainForm : Form
    {
        #region Fields

        private List<string> m_AssemblyTypes = new List<string>();
        private OpenFileDialog m_OpenFileDialog = null;
        private ViewHelper m_ViewHelper = new ViewHelper();
        private Type m_Type;
        private MethodBase m_CurrentMemberInfo = null;

        #endregion


        #region Constructors

        public MainForm()
        {
            InitializeComponent();

            comboInstanceStatic.SelectedIndex = 0;
            comboVisibility.SelectedIndex = 0;
        }

        #endregion


        #region Form's events handlers

        private void btnShowInfo_Click(object sender, EventArgs e)
        {
            DisplayInfo();
        }

        private void btnSelectAssembly_Click(object sender, EventArgs e)
        {
            if (m_OpenFileDialog == null)
            {
                m_OpenFileDialog = new OpenFileDialog();
            }

            DialogResult result = m_OpenFileDialog.ShowDialog();

            if (result == DialogResult.Cancel)
            {
                return;
            }

            txtAssemblyPath.Text = m_OpenFileDialog.FileName;

            if (LoadTypes(txtAssemblyPath.Text))
            {
                ShowTypes();
            }
            else
            {
                MessageBox.Show("No types found in\n" + txtAssemblyPath.Text);
            }
        }

        private void lstTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtTypeName.Text = lstbxTypes.SelectedItem.ToString();
        }

        private void lstbxTypes_DoubleClick(object sender, EventArgs e)
        {
            if (lstbxTypes.SelectedIndex >= 0)
            {
                txtTypeName.Text = lstbxTypes.SelectedItem.ToString();
                DisplayInfo();
            }
        }

        private void btnFilter_Click(object sender, EventArgs e)
        {
            ShowTypes();
        }

        private void btnClearFilter_Click(object sender, EventArgs e)
        {
            txtFilter.Text = "";
            ShowTypes();
        }

        private void chkWrapLines_CheckedChanged(object sender, EventArgs e)
        {
            richtxtInfo.WordWrap = chkWrapLines.Checked;
        }

        private void txtAssemblyPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                if (LoadTypes(txtAssemblyPath.Text))
                {
                    ShowTypes();
                }
                else
                {
                    MessageBox.Show("No types found in\n" + txtAssemblyPath.Text);
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void richtxtInfo_SelectionChanged(Object sender, EventArgs e)
        {
            var mi = m_ViewHelper.Get(this.richtxtInfo, this.richtxtInfo.SelectionStart);
            if (m_Type != null && mi != null && mi != m_CurrentMemberInfo)
            {
                m_CurrentMemberInfo = mi;

                this.richtxtDetails.Clear();

                var ci = mi as ConstructorInfo;
                if (ci != null)
                {
                    OutputHelper.ShowConstructorDetails(this.richtxtDetails, ci);
                    return;
                }

                bool isExtension = mi.IsDefined(typeof(ExtensionAttribute),true);

                OutputHelper.ShowMethodDetails(this.richtxtDetails, mi, m_Type, isExtension);
            }
        }

        #endregion


        #region Helper methods

        private void DisplayInfo()
        {
            if (txtTypeName.Text == String.Empty)
            {
                return;
            }

            this.m_ViewHelper.Clear();
            this.richtxtInfo.Clear();
            this.richtxtDetails.Clear();

            txtTypeName.Text = txtTypeName.Text.Trim();

            m_Type = Type.GetType(txtTypeName.Text);

            if (m_Type == null)
            {
                try
                {
                    m_Type = Assembly.LoadFile(txtAssemblyPath.Text).GetType(txtTypeName.Text);
                }
                catch
                { }
            }

            if (m_Type != null)
            {
                TypeInfo(richtxtInfo, m_Type, CombineBindingFlags());
            }
            else
            {
                MessageBox.Show("Specified type not found.");
                return;
            }
        }

        private void TypeInfo(RichTextBox box, Type t, BindingFlags flags)
        {
            OutputHelper.AddSection(box, "// Type:\n");
            OutputHelper.AddTypeName(box, t.FullName + "\n");

            if (t.IsSealed)
            {
                OutputHelper.AddInfo(box, "[sealed]\n");
            }

            OutputHelper.AddSection(box, "\n// Assembly:\n");
            OutputHelper.AddInfo(box, t.Assembly.FullName + "\n");
            OutputHelper.AddInfo(box, t.Assembly.Location + "\n");

            OutputHelper.AddSection(box, "\n// Attributes:\n");
            OutputHelper.AddInfo(box, t.Attributes.ToString() + "\n");

            OutputHelper.AddSection(box, "\n// Custom Attributes:\n");
            foreach(var ca in t.CustomAttributes)
            {
                OutputHelper.AddInfo(box, ca.ToString() + "\n");
            }

            OutputHelper.AddSection(box, "\n// Base Type:\n");
            OutputHelper.AddTypeName(box, (t.BaseType != null ? t.BaseType.FullName : "No base type") + "\n");

            OutputHelper.AddSection(box, "\n// Interfaces:\n");
            Type[] interfaces = t.GetInterfaces();
            if (interfaces.Length > 0)
            {
                foreach (Type i in interfaces)
                {
                    OutputHelper.AddTypeName(box, i.FullName + "\n");
                }
            }
            else
            {
                OutputHelper.AddInfo(box, "-\n");
            }

            OutputHelper.AddInfo(box, t.IsInterface ? "\nIs Interface\n" : "");

            OutputHelper.AddSection(box, "\n-------------------------------------------------\n");

            OutputHelper.AddSection(box, "\n// Constructors:\n");
            ConstructorInfo[] constructors = t.GetConstructors();
            if (constructors.Length > 0)
            {
                foreach (ConstructorInfo ci in constructors)
                {
                    var position = box.Text.Length;
                    OutputHelper.AddConstructorInfo(box, ci);
                    m_ViewHelper.Add(ci, position, box.Text.Length - position);
                }
            }
            else
            {
                OutputHelper.AddInfo(box, "-\n");
            }


            OutputHelper.AddSection(box, "\n// Methods:\n");
            MethodInfo[] methods = t.GetMethods(flags);

            int CompareByVisibility(MethodInfo x, MethodInfo y)
            {
                if ((x.IsPublic && y.IsPublic)
                    || (x.IsFamily && y.IsFamily)
                    || (x.IsPrivate && y.IsPrivate))
                {
                    return 0;
                }
                else if (x.IsPublic && !y.IsPublic)
                {
                    return -1;
                }
                else if (x.IsFamily && y.IsPublic)
                {
                    return 1;
                }
                else if (x.IsFamily && y.IsPrivate)
                {
                    return -1;
                }
                else // x.IsPrivate;
                {
                    return 1; // x.IsPrivate && y.IsPrivate <- the first case for comparison.
                }
            }

            Array.Sort(methods, (x, y) => {
                if (x.IsStatic && !y.IsStatic) {
                    return -1;
                }
                else if (!x.IsStatic && y.IsStatic) {
                    return 1;
                }
                else {
                    return CompareByVisibility(x, y);
                }
            });
            if (methods.Length > 0)
            {
                Array.Sort(methods, (m1, m2) => {
                    // Methods declared in parent go first.
                    if (m1.DeclaringType != m2.DeclaringType)
                    {
                        if (m1.DeclaringType == t)
                        {
                            return 1;
                        }
                        else if (m2.DeclaringType == t)
                        {
                            return -1;
                        }
                        // Something weird...
                        else
                        {
                            return 0;
                        }
                    }
                    // Then sorting methods by name.
                    else
                    {
                        return m1.Name.CompareTo(m2.Name);
                    }
                });
                foreach (MethodInfo mi in methods)
                {
                    bool isExtension = mi.IsDefined(typeof(ExtensionAttribute),true);
                    var position = box.Text.Length;
                    OutputHelper.AddMethodInfo(box, mi, t, isExtension);
                    m_ViewHelper.Add(mi, position, box.Text.Length - position);
                }
            }
            else
            {
                OutputHelper.AddInfo(box, "-\n");
            }

            OutputHelper.AddSection(box, "\n// Properties:\n");
            PropertyInfo[] properties = t.GetProperties(flags);
            if (properties.Length > 0)
            {
                foreach (PropertyInfo pi in properties)
                {
                    OutputHelper.AddPropertyInfo(box, pi);
                    box.AppendText("\n");
                }
            }
            else
            {
                OutputHelper.AddInfo(box, "-\n");
            }

            if (t.IsEnum)
            {
                OutputHelper.AddSection(box, "\n// Enum values:\n");
                var values = Enum.GetValues(t);
                foreach (var v in values)
                {
                    var name = Enum.GetName(t, v);
                    OutputHelper.AddInfo(box, name + ": " + ((int)v).ToString() + "\n");
                }
            }
        }

        private BindingFlags CombineBindingFlags()
        {
            BindingFlags flags = new BindingFlags();

            if ((string)comboInstanceStatic.SelectedItem == "Both")
            {
                flags |= BindingFlags.Instance | BindingFlags.Static;
            }
            else if ((string)comboInstanceStatic.SelectedItem == "Instance")
            {
                flags |= BindingFlags.Instance;
            }
            else if ((string)comboInstanceStatic.SelectedItem == "Static")
            {
                flags |= BindingFlags.Static;
            }

            if ((string)comboVisibility.SelectedItem == "Both")
            {
                flags |= BindingFlags.Public | BindingFlags.NonPublic;
            }
            else if ((string)comboVisibility.SelectedItem == "Public")
            {
                flags |= BindingFlags.Public;
            }
            else if ((string)comboVisibility.SelectedItem == "NonPublic")
            {
                flags |= BindingFlags.NonPublic;
            }

            if (chkDeclaredOnly.Checked)
            {
                flags |= BindingFlags.DeclaredOnly;
            }

            if (chkFlattenHierarchy.Checked)
            {
                flags |= BindingFlags.FlattenHierarchy;
            }

            return flags;
        }

        private bool LoadTypes(string assemblyFilePath)
        {
            try
            {
                m_AssemblyTypes.Clear();

                Assembly a = Assembly.LoadFile(assemblyFilePath);

                foreach (TypeInfo ti in a.DefinedTypes)
                {
                    m_AssemblyTypes.Add(ti.FullName);
                }

                return true;
            }
            catch (Exception e)
            {
                lstbxTypes.Items.Clear();
                lstbxTypes.Text = "... / ...";
                MessageBox.Show(String.Format("An error occured:\n{0}", e.ToString()));
                return false;
            }
        }

        private void ShowTypes()
        {
            lstbxTypes.Items.Clear();
            foreach (string type in m_AssemblyTypes)
            {
                if ((txtFilter.Text == "") || (txtFilter.Text != "" && type.Contains(txtFilter.Text)))
                {
                    lstbxTypes.Items.Add(type);
                }
            }

            lblCount.Text = (txtFilter.Text != "" ? lstbxTypes.Items.Count.ToString() + " of " : "")
                + m_AssemblyTypes.Count.ToString() + " types";
        }

        #endregion
    }
}
