﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySQL.Utility;
using Excel = Microsoft.Office.Interop.Excel;
using MySql.Data.MySqlClient;

namespace MySQL.ForExcel
{
  public partial class ExportDataForm : Form
  {
    private MySqlWorkbenchConnection wbConnection;
    private ExportDataHelper exportDataHelper;
    private MySQLTable exportTable { get { return exportDataHelper.ExportTable; } }
    private List<ColumnGuessData> headerRowColumnsGuessData { get { return exportDataHelper.HeaderRowColumnsGuessData; } }
    private List<ColumnGuessData> dataRowsColumnsGuessData { get { return exportDataHelper.DataRowsColumnsGuessData; } }
    private DataTable formattedExcelData { get { return exportDataHelper.FormattedExcelData; } }
    private DataTable unformattedExcelData { get { return exportDataHelper.UnformattedExcelData; } }
    private bool tableNameValidated = false;
    private bool detectDatatype = false;
    private bool addBufferToVarchar = false;
    private bool autoIndexIntColumns = false;
    private bool autoAllowEmptyNonIndexColumns = false;
    private bool multiColumnPK = false;
    private BindingList<MySQLColumn> bindColsList;

    public ExportDataForm(MySqlWorkbenchConnection wbConnection, Excel.Range exportDataRange, Excel.Worksheet exportingWorksheet)
    {
      this.wbConnection = wbConnection;

      InitializeComponent();

      exportDataHelper = new ExportDataHelper(wbConnection, exportDataRange, null, false, true);
      if (!exportingWorksheet.Name.ToLowerInvariant().StartsWith("sheet"))
        exportTable.Name = exportingWorksheet.Name;
      columnBindingSource.DataSource = exportTable.Columns;
      Text = String.Format("Export Data - {0} [{1}])", exportingWorksheet.Name, exportDataRange.Address.Replace("$", String.Empty));
      bindColsList = new BindingList<MySQLColumn>(exportTable.Columns);
      bindColsList.ListChanged += new ListChangedEventHandler(bindColsList_ListChanged);
      cmbPrimaryKeyColumns.DataSource = bindColsList;
      cmbPrimaryKeyColumns.ValueMember = "ColumnName";
      cmbPrimaryKeyColumns.DisplayMember = "ColumnName";

      loadAdvancedSettings();
      if (detectDatatype && exportTable.Columns[1].DataType.ToLowerInvariant().Contains("int"))
      {
        radUseExistingColumn.Checked = true;
        cmbPrimaryKeyColumns.SelectedIndex = 0;
      }
      initializeDataTypeCombo();
      chkFirstRowHeaders.Checked = true;

      txtTableNameInput.DataBindings.Add(new Binding("Text", exportTable, "Name"));
      txtTableNameInput.SelectAll();
    }

    private void loadAdvancedSettings()
    {
      detectDatatype = Properties.Settings.Default.ExportDetectDatatype;
      exportDataHelper.GuessDataTypesFromData(detectDatatype, true);
      addBufferToVarchar = Properties.Settings.Default.ExportAddBufferToVarchar;
      autoIndexIntColumns = Properties.Settings.Default.ExportAutoIndexIntColumns;
      autoAllowEmptyNonIndexColumns = Properties.Settings.Default.ExportAutoAllowEmptyNonIndexColumns;
      useFormattedExcelData(Properties.Settings.Default.ExportUseFormattedValues);
      btnCopySQL.Visible = Properties.Settings.Default.ExportShowCopySQLButton;
    }

    private void initializeDataTypeCombo()
    {
      DataTable dataTypesTable = new DataTable();
      dataTypesTable.Columns.Add("Value");
      dataTypesTable.Columns.Add("Description");

      dataTypesTable.Rows.Add(new string[] { "Integer", "Integer - Default for whole-number columns" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(5)", "Varchar(5) - Small string up to 5 characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(12)", "Varchar(12) - Small string up to 12 characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(25)", "Varchar(25) - Small string up to 25 characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(45)", "Varchar(45) - Standard string up to 45 characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(255)", "Varchar(255) - Standard string up to 255 characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(4000)", "Varchar(4000) - Large string up to 4k characters" });
      dataTypesTable.Rows.Add(new string[] { "Varchar(65535)", "Varchar(65535) - Maximum string up to 65k characters" });
      dataTypesTable.Rows.Add(new string[] { "Datetime", "Datetime - For columns that store both, date and time" });
      dataTypesTable.Rows.Add(new string[] { "Date", "Date - For columns that only store a date" });
      dataTypesTable.Rows.Add(new string[] { "Time", "Time - For columns that only store a time" });
      dataTypesTable.Rows.Add(new string[] { "Bool", "Bool - Holds values like (0, 1), (True, False) or (Yes, No)" });
      dataTypesTable.Rows.Add(new string[] { "BigInt", "BigInt - For columns containing large whole-number integers with up to 19 digits" });
      dataTypesTable.Rows.Add(new string[] { "Decimal(12, 2)", "Decimal(12, 2) - Exact decimal numbers with 12 digits with 2 of them after decimal point" });
      dataTypesTable.Rows.Add(new string[] { "Decimal(65, 30)", "Decimal(65, 30) - Biggest exact decimal numbers with 65 digits with 30 of them after decimal point" });
      dataTypesTable.Rows.Add(new string[] { "Double", "Double - Biggest float pointing number with approximately 15 decimal places" });

      cmbDatatype.DataSource = dataTypesTable;
      cmbDatatype.ValueMember = "Value";
      cmbDatatype.DisplayMember = "Value";
      cmbDatatype.DropDownWidth = 300;
    }

    private void refreshColumnsNameAndType()
    {
      ColumnGuessData headerColData;
      ColumnGuessData otherColData;

      for (int colIdx = 0; colIdx < exportTable.Columns.Count; colIdx++)
      {
        headerColData = headerRowColumnsGuessData[colIdx];
        otherColData = dataRowsColumnsGuessData[colIdx];
        if (exportTable.Columns[colIdx].DataType != null && exportTable.Columns[colIdx].DataType != headerColData.MySQLType && exportTable.Columns[colIdx].DataType != otherColData.MySQLType)
          continue;
        if (chkFirstRowHeaders.Checked)
        {
          exportTable.Columns[colIdx].ColumnName = headerColData.ColumnName;
          exportTable.Columns[colIdx].AssignDataType(otherColData.MySQLType, otherColData.StrLen);
        }
        else
        {
          exportTable.Columns[colIdx].ColumnName = otherColData.ColumnName;
          exportTable.Columns[colIdx].AssignDataType((headerColData.MySQLType == otherColData.MySQLType ? otherColData.MySQLType : "varchar"), otherColData.StrLen);
        }
      }
      columnBindingSource.ResetBindings(false);
    }

    private void useFormattedExcelData(bool formatted)
    {
      grdPreviewData.DataSource = (formatted ? formattedExcelData : unformattedExcelData);
      foreach (DataGridViewColumn gridCol in grdPreviewData.Columns)
      {
        gridCol.SortMode = DataGridViewColumnSortMode.NotSortable;
      }
      grdPreviewData.SelectionMode = DataGridViewSelectionMode.FullColumnSelect;
      chkFirstRowHeaders_CheckedChanged(chkFirstRowHeaders, EventArgs.Empty);
    }

    private void showValidationWarning(string warningControlSuffix, bool show, string text)
    {
      string picBoxName = String.Format("pic{0}", warningControlSuffix);
      string lblName = String.Format("lbl{0}", warningControlSuffix);

      if (!ExportDataPanel.Controls.ContainsKey(picBoxName) || !ExportDataPanel.Controls.ContainsKey(lblName))
        return;

      ExportDataPanel.Controls[picBoxName].Visible = show;
      if (!String.IsNullOrEmpty(text))
        ExportDataPanel.Controls[lblName].Text = text;
      ExportDataPanel.Controls[lblName].Visible = show;
    }

    private void flagMultiColumnPrimaryKey(bool multiColPK)
    {
      radAddPrimaryKey.Checked = false;
      radUseExistingColumn.Checked = multiColPK;
      cmbDatatype.Text = (multiColPK ? "<Multiple columns>" : String.Empty);
      cmbDatatype.Enabled = !multiColPK;
    }

    private void addPrimaryKeyColumn(bool add)
    {
      if (add)
      {
        MySQLColumn pkCol = new MySQLColumn(null, exportTable);
        pkCol.ColumnName = txtAddPrimaryKey.Text;
        pkCol.PrimaryKey = true;
        pkCol.DataType = "Integer";
        pkCol.AutoIncrement = true;
        exportTable.Columns.Insert(0, pkCol);
      }
      else
      {
        if (exportTable.Columns[0].ColumnName.ToLowerInvariant() == txtAddPrimaryKey.Text.ToLowerInvariant())
          exportTable.Columns.RemoveAt(0);
      }
    }

    private void btnCopySQL_Click(object sender, EventArgs e)
    {

    }

    private void btnExport_Click(object sender, EventArgs e)
    {
      bool success = exportDataHelper.CreateTableInDB();
      success = success && exportDataHelper.InsertDataWithAdapter(chkFirstRowHeaders.Checked, Properties.Settings.Default.ExportUseFormattedValues);
      if (success)
      {
        DialogResult = DialogResult.OK;
        Close();
      }
    }

    private void btnAdvanced_Click(object sender, EventArgs e)
    {
      ExportAdvancedOptionsDialog optionsDialog = new ExportAdvancedOptionsDialog();
      DialogResult dr = optionsDialog.ShowDialog();
      if (dr == DialogResult.OK)
        loadAdvancedSettings();
    }

    private void chkFirstRowHeaders_CheckedChanged(object sender, EventArgs e)
    {
      if (exportTable == null || exportTable.Columns.Count == 0)
        return;
      refreshColumnsNameAndType();
      for (int colIdx = 0; colIdx < grdPreviewData.Columns.Count; colIdx++)
      {
        MySQLColumn mysqlCol = exportTable.Columns[colIdx];
        DataGridViewColumn gridCol = grdPreviewData.Columns[colIdx];
        gridCol.HeaderText = mysqlCol.ColumnName;
      }
      grdPreviewData.CurrentCell = null;
      grdPreviewData.Rows[0].Visible = !chkFirstRowHeaders.Checked;
      if (chkFirstRowHeaders.Checked && grdPreviewData.Rows.Count < 2)
        return;
      grdPreviewData.FirstDisplayedScrollingRowIndex = (chkFirstRowHeaders.Checked ? 1 : 0);
    }

    private void txtTableNameInput_Validating(object sender, CancelEventArgs e)
    {
      timerTextChanged.Stop();

      bool showWarning = (txtTableNameInput.Text.Contains(" ") || txtTableNameInput.Text.Any(char.IsUpper));
      showValidationWarning("TableNameWarning", showWarning, Properties.Resources.NamesWarning);

      string cleanTableName = txtTableNameInput.Text.ToLowerInvariant().Replace(" ", "_");
      bool tableExistsInSchema = Utilities.TableExistsInSchema(wbConnection, wbConnection.Schema, cleanTableName);

      if (tableExistsInSchema)
      {
        showValidationWarning("TableNameWarning", true, Properties.Resources.TableNameExistsWarning);
        btnExport.Enabled = false;
      }
      else
      {
        showValidationWarning("TableNameWarning", showWarning, null);
        btnExport.Enabled = true;
      }
    }

    private void timerTextChanged_Tick(object sender, EventArgs e)
    {
      if (txtTableNameInput.Text.Length == 0)
      {
        timerTextChanged.Stop();
        return;
      }
      txtTableNameInput_Validating(txtTableNameInput, new CancelEventArgs());
    }

    private void txtTableNameInput_KeyDown(object sender, KeyEventArgs e)
    {
      timerTextChanged.Stop();
    }

    private void txtTableNameInput_KeyUp(object sender, KeyEventArgs e)
    {
      timerTextChanged.Start();
    }

    private void radAddPrimaryKey_CheckedChanged(object sender, EventArgs e)
    {
      grdPreviewData.Columns[0].Visible = radAddPrimaryKey.Checked;
      cmbPrimaryKeyColumns.Enabled = false;
      txtAddPrimaryKey.Enabled = true;
      if (txtAddPrimaryKey.Text.Length == 0)
        txtAddPrimaryKey.Text = String.Format("{0}_id", exportTable.Name);
    }

    private void radUseExistingColumn_CheckedChanged(object sender, EventArgs e)
    {
      cmbPrimaryKeyColumns.Enabled = true;
      txtAddPrimaryKey.Enabled = false;
    }

    private void txtAddPrimaryKey_Validating(object sender, CancelEventArgs e)
    {
      bool showWarning = exportTable.Columns.Any(col => col.ColumnName.ToLowerInvariant() == txtAddPrimaryKey.Text.ToLowerInvariant());
      btnExport.Enabled = !showWarning;
      showValidationWarning("PrimaryKeyWarning", showWarning, Properties.Resources.PrimaryKeyColumnExistsWarning);
    }

    private void bindColsList_ListChanged(object sender, ListChangedEventArgs e)
    {
      if (e.PropertyDescriptor.Name != "PrimaryKey")
        return;
      List<MySQLColumn> realColumns = new List<MySQLColumn>(exportTable.Columns);
      realColumns.RemoveAt(0);
      bool multiKey = realColumns.Count(col => col.PrimaryKey) > 1;
      if (multiKey && !multiColumnPK)
        flagMultiColumnPrimaryKey(true);
      else if (!multiKey && multiColumnPK)
        flagMultiColumnPrimaryKey(false);
      multiColumnPK = multiKey;
    }

    private void chkUniqueIndex_CheckedChanged(object sender, EventArgs e)
    {
      chkCreateIndex.Checked = chkUniqueIndex.Checked;
    }

    private void columnBindingSource_CurrentChanged(object sender, EventArgs e)
    {
      if (columnBindingSource.Current != null)
        grdPreviewData.Columns[columnBindingSource.Position].Selected = true;
    }

    private void grdPreviewData_SelectionChanged(object sender, EventArgs e)
    {
      if (grdPreviewData.SelectedColumns.Count > 0)
        columnBindingSource.Position = grdPreviewData.SelectedColumns[0].DisplayIndex;
    }

    private void columnBindingSource_BindingComplete(object sender, BindingCompleteEventArgs e)
    {
      grdPreviewData.Columns[0].Visible = radAddPrimaryKey.Checked;
      if (txtTableNameInput.Text.Length > 0)
        timerTextChanged.Start();
    }

    private void cmbDatatype_DropDown(object sender, EventArgs e)
    {
      cmbDatatype.DisplayMember = "Description";
    }

    private void cmbDatatype_DropDownClosed(object sender, EventArgs e)
    {
      cmbDatatype.DisplayMember = "Value";
    }

    private void cmbPrimaryKeyColumns_SelectedIndexChanged(object sender, EventArgs e)
    {
      
    }

    private void cmbPrimaryKeyColumns_Validating(object sender, CancelEventArgs e)
    {
      if (multiColumnPK)
      {
        DialogResult dr = Utilities.ShowWarningBox("Do you want to reset the Primary Key to the single selected column?");
        if (dr == DialogResult.No)
        {
          e.Cancel = true;
          return;
        }
        multiColumnPK = false;
        List<MySQLColumn> realColumns = new List<MySQLColumn>(exportTable.Columns);
        realColumns.RemoveAt(0);
        foreach (MySQLColumn col in realColumns)
        {
          if (col.ColumnName == cmbPrimaryKeyColumns.Text)
            continue;
          col.PrimaryKey = false;
        }
      }
      else
        exportTable.Columns.Single(col => col.ColumnName == cmbPrimaryKeyColumns.Text).PrimaryKey = true;
    }

  }
}