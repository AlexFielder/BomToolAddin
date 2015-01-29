Option Explicit On

Imports Inventor
Imports System.Runtime.InteropServices
Imports Microsoft.Win32
Imports System.Linq
Imports System.Text
Imports System.Collections.Generic
Imports System.Windows.Forms
Imports System.Text.RegularExpressions

Namespace BomToolAddin
    <ProgIdAttribute("BomToolAddin.StandardAddInServer"), _
    GuidAttribute("4020c616-4cca-49ce-8ed6-64fd15c6e2a7")> _
    Public Class StandardAddInServer
        Implements Inventor.ApplicationAddInServer

        ' Inventor application object.
        Private m_inventorApplication As Inventor.Application

#Region "ApplicationAddInServer Members"

        Public Sub Activate(ByVal addInSiteObject As Inventor.ApplicationAddInSite, ByVal firstTime As Boolean) Implements Inventor.ApplicationAddInServer.Activate

            ' This method is called by Inventor when it loads the AddIn.
            ' The AddInSiteObject provides access to the Inventor Application object.
            ' The FirstTime flag indicates if the AddIn is loaded for the first time.

            ' Initialize AddIn members.
            m_inventorApplication = addInSiteObject.Application

            ' TODO:  Add ApplicationAddInServer.Activate implementation.
            ' e.g. event initialization, command creation etc.

        End Sub

        Public Sub Deactivate() Implements Inventor.ApplicationAddInServer.Deactivate

            ' This method is called by Inventor when the AddIn is unloaded.
            ' The AddIn will be unloaded either manually by the user or
            ' when the Inventor session is terminated.

            ' TODO:  Add ApplicationAddInServer.Deactivate implementation

            ' Release objects.
            m_inventorApplication = Nothing

            System.GC.Collect()
            System.GC.WaitForPendingFinalizers()
        End Sub

        Public ReadOnly Property Automation() As Object Implements Inventor.ApplicationAddInServer.Automation

            ' This property is provided to allow the AddIn to expose an API 
            ' of its own to other programs. Typically, this  would be done by
            ' implementing the AddIn's API interface in a class and returning 
            ' that class object through this property.

            Get
                Return Nothing
            End Get

        End Property

        Public Sub ExecuteCommand(ByVal commandID As Integer) Implements Inventor.ApplicationAddInServer.ExecuteCommand

            ' Note:this method is now obsolete, you should use the 
            ' ControlDefinition functionality for implementing commands.

        End Sub

#End Region
        ''' <summary>
        ''' Our Main program.
        ''' </summary>
        ''' <remarks></remarks>
        Sub Main()
            'Dim XTCS As New BomTool.Class1
            'Dim PartsList As List(Of BomTool.BomRowItem)
            Dim PartsList As List(Of BomRowItem)
            'pass the local variables to our external .dll
            'XTCS.InventorApplication = m_inventorApplication
            Dim oDoc As AssemblyDocument
            Dim oDrawDoc As DrawingDocument
            ' Set a reference to the BOM
            Dim oBOM As BOM
            BOMSpreadsheetName = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(m_inventorApplication.ActiveDocument.FullDocumentName)) & "\DRGS\BOM-" & GetFriendlyName(System.IO.Path.GetFileNameWithoutExtension(m_inventorApplication.ActiveDocument.FullFileName)) & ".xlsx"
            If TypeOf m_inventorApplication.ActiveDocument Is AssemblyDocument Then
                oDoc = m_inventorApplication.ActiveDocument
                oBOM = oDoc.ComponentDefinition.BOM
            ElseIf TypeOf m_inventorApplication.ActiveDocument Is DrawingDocument Then
                oDrawDoc = m_inventorApplication.ActiveDocument
                'MessageBox.Show("Skipping BOM Creation since you already did that bit didn't you?", "Being sarcastic, of course you did, right!?")
                MessageBox.Show("Looking for: " & BOMSpreadsheetName)
                UpdateBOMSpreadsheet(BOMSpreadsheetName)
                Exit Sub
            End If
            'Dim oDoc As AssemblyComponentDefinition = m_inventorApplication.ActiveDocument.ComponentDefinition


            oBOM.StructuredViewFirstLevelOnly = True

            ' Make sure that the structured view is enabled.
            oBOM.StructuredViewEnabled = True

            'Set a reference to the "Structured" BOMView
            Dim oBOMView As BOMView
            oBOMView = oBOM.BOMViews.Item("Structured")

            Dim tr As Transaction
            tr = m_inventorApplication.TransactionManager.StartTransaction(m_inventorApplication.ActiveDocument, "Create Excel Bom From This Assembly")
            Classification = InputBox("Classification?", "Hit N,C & Tab...", "UKU")
            'PartsList = New List(Of BomTool.BomRowItem)
            PartsList = New List(Of BomRowItem)
            PartsList.Add(getRowItem(oDoc.ComponentDefinition))
            PartsList.AddRange(QueryBOMRowProperties(oBOMView.BOMRows))
            'Call XTCS.BeginReformatBomForExcel(PartsList)
            BeginReformatBomForExcel(PartsList)
            'Call XTCS.UpdateInventorPartsList(oBOMView.BOMRows, PartsList)
            UpdateInventorPartsList(oBOMView.BOMRows, PartsList)
            oBOMView.Sort("Item", 1)
            ReturnPartsListToExcel(PartsList)
            tr.End()
            m_inventorApplication.ActiveView.Update()
        End Sub

        Public BOMSpreadsheetName As String
        'Public XTCS As New BomTool.Class1
        Public Classification As String = String.Empty
        'Public PartsList As List(Of BomTool.BomRowItem)
        'Public BomList As list(Of BomTool.BomRowItem)
        Public PartsList As List(Of BomRowItem)
        Public BomList As List(Of BomRowItem)
        Public numHeaderRows As Integer

#Region "BOM Spreadsheet Creation"


        ''' <summary>
        ''' Queries the BOM Row properties and generates a list to pass externally for sorting.
        ''' </summary>
        ''' <param name="oBOMRows"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function QueryBOMRowProperties(ByVal oBOMRows As BOMRowsEnumerator) As List(Of BomRowItem)
            Dim tmplist As List(Of BomRowItem) = New List(Of BomRowItem)
            ' Iterate through the contents of the BOM Rows.
            Dim i As Long
            For i = 1 To oBOMRows.Count

                ' Get the current row.
                Dim oRow As BOMRow
                oRow = oBOMRows.Item(i)

                'Set a reference to the primary ComponentDefinition of the row
                Dim oCompDef As ComponentDefinition
                oCompDef = oRow.ComponentDefinitions.Item(1)
                tmplist.Add(getRowItem(oCompDef, oRow))
            Next
            Return tmplist
        End Function

        ''' <summary>
        ''' Begins the reformatting of the Inventor BOM
        ''' </summary>
        ''' <param name="InventorBomList"></param>
        Public Sub BeginReformatBomForExcel(ByRef InventorBomList As List(Of BomRowItem))
            'MessageBox.Show("Inventor Bom list count =" + InventorBomList.Count);
            BomList = New List(Of BomRowItem)
            Dim grouped = InventorBomList.OrderBy(Function(x) x.BomRowType).ThenBy(Function(x) x.PartNo).GroupBy(Function(x) x.BomRowType)
            'InventorBomList.RemoveRange(0, InventorBomList.Count);
            ' InventorBomList.RemoveAll(NotEmpty);
            'MessageBox.Show("InventorBomList.Count= " + InventorBomList.Count);
            Dim SubAssemblyInt As Integer = 1
            Dim DetailedPartsInt As Integer = 200
            Dim COTSContentImportedInt As Integer = 500
            For Each group As Object In grouped
                'group.OrderBy(Function(x) x.PartNo)
                For Each item As BomRowItem In group
                    Select Case item.BomRowType
                        Case 0
                            'Parent Assembly
                            'MessageBox.Show("Should only be one of these!");
                            item.ItemNo = 0
                            BomList.Add(item)
                            Exit Select
                        Case 1
                            'Specifications = no item number
                            item.ItemNo = 9999
                            BomList.Add(item)
                            Exit Select
                        Case 2
                            ' Sub assemblies = 1 to 199
                            item.ItemNo = SubAssemblyInt
                            SubAssemblyInt += 1
                            BomList.Add(item)
                            Exit Select
                        Case 3
                            ' Detailed Parts = 200 to 500
                            item.ItemNo = DetailedPartsInt
                            DetailedPartsInt += 1
                            BomList.Add(item)
                            Exit Select
                        Case 4
                            ' COTS Parts/Content Centre/Imported Components = 500 to 999
                            item.ItemNo = COTSContentImportedInt
                            COTSContentImportedInt += 1
                            BomList.Add(item)
                            Exit Select
                        Case Else
                            Exit Select
                    End Select
                Next
            Next
            'hopefully sort by ItemNo
            BomList.OrderBy(Function(x) x.ItemNo)
            'MessageBox.Show("BomList.Count= " + BomList.Count);
            InventorBomList = BomList
        End Sub

        ''' <summary>
        ''' Updates the Inventor item number.
        ''' </summary>
        ''' <param name="oBOMROWs">the BOMROWs collection</param>
        ''' <param name="oSortedPartsList"></param>
        Public Sub UpdateInventorPartsList(oBOMROWs As BOMRowsEnumerator, oSortedPartsList As List(Of BomRowItem))
            'MessageBox.Show("Reached UpdateInventorPartsList Sub");
            Dim oCompdef As ComponentDefinition
            For Each oRow As BOMRow In oBOMROWs
                oCompdef = oRow.ComponentDefinitions(1)
                Dim item As Long = (From a In oSortedPartsList Where a.FileName = oCompdef.Document.FullFileName Select a.ItemNo).FirstOrDefault()
                If item = 0 OrElse item = 9999 Then
                    oRow.ItemNumber = ""
                Else
                    oRow.ItemNumber = item.ToString()
                End If
            Next
        End Sub


        ''' <summary>
        ''' creates a BomRowItem for every ComponentDefinition passed to it.
        ''' </summary>
        ''' <param name="oCompdef">the ComponentDefinition we need to query against.</param>
        ''' <returns>Returns a BomRowItem</returns>
        ''' <remarks></remarks>
        Public Function getRowItem(oCompdef As ComponentDefinition, oRow As BOMRow) As BomRowItem

            'get the PropertySets we need
            Dim invProjProperties As PropertySet = oCompdef.Document.PropertySets.Item("{32853F0F-3444-11D1-9E93-0060B03C1CA6}")
            Dim invSummaryiProperties As PropertySet = oCompdef.Document.PropertySets.Item("{F29F85E0-4FF9-1068-AB91-08002B27B3D9}")
            Dim invCustomPropertySet As PropertySet = oCompdef.Document.PropertySets.Item("Inventor User Defined Properties")

            Dim oPartNumProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kPartNumberDesignTrackingProperties).Value
            Dim oRevProperty As String = invSummaryiProperties.ItemByPropId(PropertiesForSummaryInformationEnum.kRevisionSummaryInformation).Value
            If oRevProperty = "" Then
                oRevProperty = "-"
            End If
            Dim oDescripProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kDescriptionDesignTrackingProperties).Value
            Dim oStatusProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kDesignStatusDesignTrackingProperties).Value

            Dim oItemNo As String = oRow.ItemNumber
            Dim oClassification As String = Classification
            Dim oMaterial As String
            If TypeOf oCompdef.Document Is AssemblyDocument Then
                oMaterial = "-"
            Else
                oMaterial = oCompdef.Material.Name
            End If
            Dim oQty As Long = oRow.ItemQuantity
            Dim oVendorProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kVendorDesignTrackingProperties).Value
            If oVendorProperty = "" Or oVendorProperty = "Supplier/Manufacturer" Then
                oVendorProperty = "-"
            End If
            Dim oCommentsProperty As String = invSummaryiProperties.ItemByPropId(PropertiesForSummaryInformationEnum.kCommentsSummaryInformation).Value
            If oCommentsProperty = "" Then
                oCommentsProperty = "-"
            End If

            'Dim rowItem As BomTool.BomRowItem = New BomTool.BomRowItem()
            Dim rowItem As BomRowItem = New BomRowItem()

            rowItem.FileName = oCompdef.Document.FullFileName
            rowItem.PartNo = oPartNumProperty
            rowItem.Rev = oRevProperty
            rowItem.Descr = oDescripProperty
            rowItem.ItemNo = oItemNo
            rowItem.Classification = oClassification
            rowItem.Material = oMaterial
            rowItem.Qty = oQty
            rowItem.Vendor = oVendorProperty
            rowItem.Comments = oCommentsProperty
            rowItem.BomRowType = GetBomRowTypeByFileName(oCompdef.Document.FullFileName)
            rowItem.status = oStatusProperty
            Return rowItem
        End Function

        ''' <summary>
        ''' creates a BomRowItem for the parent ComponentDefinition passed to it.
        ''' </summary>
        ''' <param name="oCompdef">the ComponentDefinition we need to query against.</param>
        ''' <returns>Returns a BomRowItem</returns>
        ''' <remarks></remarks>
        Public Function getRowItem(ByVal oCompdef As ComponentDefinition) As BomRowItem
            'MessageBox.Show("Parent Assembly= " & oCompDef.Document.FullFileName)
            'get the PropertySets we need
            Dim invProjProperties As PropertySet = oCompdef.Document.PropertySets.Item("{32853F0F-3444-11D1-9E93-0060B03C1CA6}")
            Dim invSummaryiProperties As PropertySet = oCompdef.Document.PropertySets.Item("{F29F85E0-4FF9-1068-AB91-08002B27B3D9}")
            Dim invCustomPropertySet As PropertySet = oCompdef.Document.PropertySets.Item("Inventor User Defined Properties")

            Dim oPartNumProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kPartNumberDesignTrackingProperties).Value
            Dim oRevProperty As String = invSummaryiProperties.ItemByPropId(PropertiesForSummaryInformationEnum.kRevisionSummaryInformation).Value
            If oRevProperty = "" Then
                oRevProperty = "-"
            End If
            Dim oDescripProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kDescriptionDesignTrackingProperties).Value
            Dim oStatusProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kDesignStatusDesignTrackingProperties).Value
            Dim oVendorProperty As String = invProjProperties.ItemByPropId(PropertiesForDesignTrackingPropertiesEnum.kVendorDesignTrackingProperties).Value
            If oVendorProperty = "" Or oVendorProperty = "Supplier/Manufacturer" Then
                oVendorProperty = "-"
            End If
            Dim oCommentsProperty As String = invSummaryiProperties.ItemByPropId(PropertiesForSummaryInformationEnum.kCommentsSummaryInformation).Value
            If oCommentsProperty = "" Then
                oCommentsProperty = "-"
            End If
            'Dim rowItem As BomTool.BomRowItem = New BomTool.BomRowItem()
            Dim rowItem As BomRowItem = New BomRowItem()

            rowItem.FileName = oCompdef.Document.FullFileName
            rowItem.PartNo = oPartNumProperty
            rowItem.Rev = oRevProperty
            rowItem.Descr = oDescripProperty
            rowItem.ItemNo = 0
            rowItem.Classification = Classification
            rowItem.Material = "-"
            rowItem.Qty = 1
            rowItem.Vendor = oVendorProperty
            rowItem.Comments = oCommentsProperty
            rowItem.BomRowType = GetBomRowTypeByFileName(oCompdef.Document.FullFileName)
            rowItem.status = oStatusProperty
            Return rowItem
        End Function

        ''' <summary>
        ''' Returns the BomRowType int
        ''' </summary>
        ''' <param name="DocName">The name to check against.</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function GetBomRowTypeByFileName(DocName As String) As Long
            If DocName.Contains("SP-") Then
                'MessageBox.Show(DocName & " Returns value 1")
                Return 1
            ElseIf DocName.EndsWith(".iam") And Not DocName = m_inventorApplication.ActiveDocument.FullFileName Then
                'MessageBox.Show(DocName & " Returns value 2")
                Return 2
            ElseIf DocName.Contains("DT-") Then
                'MessageBox.Show(DocName & " Returns value 3")
                Return 3
            ElseIf DocName.Contains("COTS-") Or DocName.Contains("Content") Or DocName.Contains("Imported Components") Then
                'MessageBox.Show(DocName & " Returns value 4")
                Return 4
            ElseIf DocName = m_inventorApplication.ActiveDocument.FullFileName Then
                'MessageBox.Show(DocName & " Returns value 0")
                Return 0
            Else 'These are parts which don't match any of the above criteria
                'MessageBox.Show(DocName & " Returns value 4")
                Return 4
            End If
        End Function

        ''' <summary>
        ''' Returns our sorted list to Excel or leaves it as-is if we know the parts list can fit on drawing sheet one of the assembly.
        ''' </summary>
        ''' <param name="oPartsList">our sorted, renumbered partslist object</param>
        ''' <remarks></remarks>
        Public Sub ReturnPartsListToExcel(ByVal oPartsList As List(Of BomRowItem))
            Dim res As Boolean
            If MessageBox.Show("Will the Parts list fit on the drawing?", "Title", MessageBoxButtons.YesNo) = DialogResult.Yes Then
                'we're done here because the parts list will fit.
                Exit Sub
            Else
                'we need to make an Excel file.
                Dim filetab As String = "DETAILS"
                Dim xlTemplate As String = "C:\LEGACY VAULT WORKING FOLDER\Designs\iLogic\Templates\BOM_Master.xlsx"
                Dim excelApp = CreateObject("Excel.Application")
                Dim excelWorkBook As Object
                Dim excelWorkSheet As Object
                excelApp.Visible = True
                excelApp.DisplayAlerts = False

                If Dir(BOMSpreadsheetName) <> "" Then
                    'MessageBox.Show("Existing Excel file found for output, updating!")
                    excelWorkBook = excelApp.workbooks.Open(BOMSpreadsheetName)
                    ReFormatBOMFrontSheet(excelApp, excelWorkBook, 200)
                    TrimDataSheet(excelApp, excelWorkBook, oPartsList.Count)
                    excelWorkSheet = excelWorkBook.Worksheets(2).Activate
                Else
                    'MessageBox.Show("New excel file underway!")
                    Try
                        excelWorkBook = excelApp.workbooks.Open(xlTemplate)
                    Catch ex As Exception
                        MessageBox.Show("You need to GET the BOM_Master.xlsx file from Vault and try again!")
                        Exit Sub
                    End Try
                    excelWorkSheet = excelWorkBook.Worksheets(2).Activate
                End If
                'FilesArray = GoExcel.CellValues("C:\LEGACY VAULT WORKING FOLDER\Designs\iLogic\Templates\BOM_Master.xlsx", filetab, "A3", "A4") ' sets excel to the correct sheet!
                Dim MyRow As Integer = 2
                numHeaderRows = 1
                Dim parentHeader As Boolean = False
                Dim specHeader As Boolean = False
                Dim saHeader As Boolean = False
                Dim dpHeader As Boolean = False
                Dim cotsHeader As Boolean = False
                For Each oItem As BomRowItem In oPartsList
                    If oItem.ItemNo = 0 And parentHeader = False Then
                        'MessageBox.Show("Parent Assembly found, filling out details!")
                        excelWorkSheet = excelWorkBook.Worksheets(1).Activate
                        excelApp.range("F1").Select()
                        excelApp.ActiveCell.Value = oItem.status
                        If oItem.Descr.Length > 30 Then '30 allows some wiggle room for when "fat" characters are used.
                            Dim lines = SplitToLines(oItem.Descr, 30)
                            Dim row As Integer = 2
                            For index = 0 To lines.Count - 1
                                excelApp.range("H" & row).Select()
                                excelApp.ActiveCell.Value = lines(index)
                                row += 1
                            Next
                        Else
                            excelApp.range("H2").Select()
                            excelApp.ActiveCell.Value = oItem.Descr
                        End If
                        excelApp.Range("K2").Select() 'Drawing ref
                        excelApp.ActiveCell.Value = System.IO.Path.GetFileNameWithoutExtension(BOMSpreadsheetName)
                        excelApp.Range("K3").Select() 'Revision
                        excelApp.ActiveCell.Value = "REVISION : " & oItem.Rev
                        excelApp.Range("K3").Characters(12, 1).Font.Bold = True

                        excelWorkSheet = excelWorkBook.Worksheets(2).Activate
                        excelApp.range("A" & MyRow).Select()
                        excelApp.ActiveCell.Value = "PARENT ASSEMBLY"
                        parentHeader = True
                        MyRow += 1
                        'NumHeaderRows += 1 'NumHeaderRows will always be at least 1
                    ElseIf oItem.ItemNo = 9999 And specHeader = False Then
                        excelApp.range("A" & MyRow).Select()
                        excelApp.ActiveCell.Value = "SPECIFICATIONS"
                        specHeader = True
                        MyRow += 1
                        numHeaderRows += 1
                    ElseIf oItem.ItemNo >= 1 And oItem.ItemNo < 200 And saHeader = False Then
                        excelApp.range("A" & MyRow).Select()
                        excelApp.ActiveCell.Value = "SUB ASSEMBLIES"
                        saHeader = True
                        MyRow += 1
                        numHeaderRows += 1
                    ElseIf oItem.ItemNo >= 200 And oItem.ItemNo < 500 And dpHeader = False Then
                        excelApp.range("A" & MyRow).Select()
                        excelApp.ActiveCell.Value = "DETAILED PARTS"
                        dpHeader = True
                        MyRow += 1
                        numHeaderRows += 1
                    ElseIf oItem.ItemNo >= 500 And cotsHeader = False Then
                        excelApp.range("A" & MyRow).Select()
                        excelApp.ActiveCell.Value = "COTS PARTS"
                        cotsHeader = True
                        MyRow += 1
                        numHeaderRows += 1
                    End If
                    excelApp.range("A" & MyRow).Select()
                    If oItem.ItemNo = 9999 Or oItem.ItemNo = 0 Then
                        excelApp.ActiveCell.Value = ""
                    Else
                        excelApp.ActiveCell.Value = oItem.ItemNo.ToString()
                    End If
                    AssignValuesToExcel(excelApp, oItem, MyRow)
                    MyRow = MyRow + 1
                Next
                MessageBox.Show("Excel output complete, saving...")
                excelApp.Columns.Autofit()
                FormatBOMFrontSheet(excelApp, excelWorkBook, MyRow)
                excelWorkBook.SaveAs(BOMSpreadsheetName)
                excelApp.quit()
                excelWorkSheet = Nothing
                excelWorkBook = Nothing
                excelApp = Nothing
            End If
        End Sub

        ''' <summary>
        ''' AssignValues to Excel
        ''' </summary>
        ''' <param name="excelapp"></param>
        ''' <param name="oItem"></param>
        ''' <param name="MyRow"></param>
        ''' <remarks></remarks>
        Public Sub AssignValuesToExcel(ByRef excelapp As Object, ByVal oItem As BomRowItem, ByVal MyRow As Integer)
            excelapp.range("B" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.PartNo
            excelapp.range("C" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Rev
            excelapp.range("D" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Descr
            excelapp.range("E" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Classification
            excelapp.range("F" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Material
            excelapp.range("G" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Qty.ToString()
            excelapp.range("H" & MyRow).Select()
            excelapp.ActiveCell.Value = "-"
            excelapp.range("I" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Vendor
            excelapp.range("J" & MyRow).Select()
            excelapp.ActiveCell.Value = "-"
            excelapp.range("K" & MyRow).Select()
            excelapp.ActiveCell.Value = oItem.Comments
        End Sub
        ''' <summary>
        ''' Returns a "Friendly" filename for comparison-sake
        ''' </summary>
        ''' <param name="p">the String to match against</param>
        ''' <returns>Returns the matched String</returns>
        ''' <remarks></remarks>
        Public Function GetFriendlyName(p As String) As Object
            Dim f As String = String.Empty
            Dim r As New Regex("\d{5,}|\w\d{5,}")
            Try
                f = r.Match(p).Captures(0).ToString() + "-000"
            Catch ex As Exception
                'no match
                f = p
            End Try
            Console.WriteLine(f)
            Return f
        End Function

        ''' <summary>
        ''' Formats the BOM Front sheet so that the cells denoting section headings can successfully overlap the adjacent cells
        ''' </summary>
        ''' <param name="Excelapp">The Excel object</param>
        ''' <param name="ExcelWorkbook">the Excel workbook</param>
        ''' <param name="NumUsedRows">the number of rows to add</param>
        ''' <remarks></remarks>
        Public Sub FormatBOMFrontSheet(ByRef Excelapp As Object, ByRef ExcelWorkbook As Object, ByVal NumUsedRows As Integer)
            'MessageBox.Show("Preparing to format BOM Front sheet")
            Dim excelWorkSheet = ExcelWorkbook.Worksheets(1).Activate 'activate front sheet
            For index = 5 To NumUsedRows + 5
                Excelapp.range("A" & index).Select()
                Dim num As Integer
                Dim IsNumeric As Boolean = Integer.TryParse(Excelapp.ActiveCell.Value, num)
                If Not IsNumeric Then
                    Excelapp.range("B" & index).Select()
                    Try
                        If Excelapp.ActiveCell.Value = 0 Then
                            Excelapp.Activecell.formula = ""
                        End If
                    Catch ex As Exception
                        If Excelapp.ActiveCell.Value = "" Then
                            Excelapp.Activecell.formula = ""
                        End If
                    End Try

                    'excelapp.ActiveCell.Value = ""
                End If
            Next
        End Sub

        ''' <summary>
        ''' Reformats the BOM Front sheet in case we've added an item to the Assembly.
        ''' </summary>
        ''' <param name="Excelapp">The Excel object</param>
        ''' <param name="ExcelWorkbook">the Excel workbook</param>
        ''' <param name="NumAvailableRows">the number of rows to reformat</param>
        ''' <remarks></remarks>
        Public Sub ReFormatBOMFrontSheet(ByRef Excelapp As Object, ByRef ExcelWorkbook As Object, ByVal NumAvailableRows As Integer)
            Dim excelWorkSheet = ExcelWorkbook.Worksheets(1).Activate 'activate front sheet

            For index = 5 To NumAvailableRows - 5
                Excelapp.range("A" & index).Select()
                Excelapp.activecell.formula = "=IF(INDIRECT(""DETAILS!A""&BOM!$L" & index & ",TRUE)<>"""",INDIRECT(""DETAILS!A""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("B" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!B""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("C" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!C""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("D" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!D""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("E" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!E""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("F" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!F""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("G" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!G""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("H" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!H""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("I" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!I""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("J" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!J""&BOM!$L" & index & ",TRUE),"""")"
                Excelapp.range("K" & index).Select()
                Excelapp.activecell.formula = "=IF(NOT(ISBLANK(INDIRECT(""DETAILS!B""&BOM!$L" & index & "))),INDIRECT(""DETAILS!K""&BOM!$L" & index & ",TRUE),"""")"
            Next

        End Sub

        ''' <summary>
        ''' Trims the Data on the Details sheet if we've edited the Assembly.
        ''' </summary>
        ''' <param name="ExcelApp"></param>
        ''' <param name="ExcelWorkbook"></param>
        ''' <param name="NumRowsRequired"></param>
        ''' <remarks></remarks>
        Public Sub TrimDataSheet(ByRef ExcelApp As Object, ByRef ExcelWorkbook As Object, ByVal NumRowsRequired As Integer)
            Dim excelWorkSheet = ExcelWorkbook.Worksheets(2).Activate 'activate data sheet
            'MessageBox.Show("Number of Rows Required = " & NumRowsRequired)
            'MessageBox.Show("Number of Header Rows Required = " & NumHeaderRows)
            If NumRowsRequired >= 200 Then
                MessageBox.Show("MaxRows needs to be increased to more than 200")
                Exit Sub
            End If
            For index = NumRowsRequired + 1 + numHeaderRows To 200 '+ 6 to account for text header rows.
                For Each c As Char In "ABCDEFGHIJKL"
                    ExcelApp.range(c & index).Select()
                    ExcelApp.ActiveCell.Value = ""
                Next
                'For c As Char = "A"c To "L"c
                '    excelapp.range(c & index).Select()
                '    excelapp.ActiveCell.Value = ""
                'Next
            Next
        End Sub

        ''' <summary>
        ''' Splits a string into lines based on max length
        ''' </summary>
        ''' <param name="stringToSplit">the string we want to split</param>
        ''' <param name="maxLineLength">int to determine max line length</param>
        ''' <returns>an IEnumerable containing string values</returns>
        ''' <remarks>copied from this page: https://stackoverflow.com/questions/22368434/best-way-to-split-string-into-lines-with-maximum-length-without-breaking-words 
        ''' - had to modify it to be a List instead of an Enumerable as List allows the Count() </remarks>
        Private Shared Iterator Function SplitToLines(stringToSplit As String, maxLineLength As Integer) As IEnumerable(Of String)
            Dim array As String() = stringToSplit.Split(New Char() {" "})
            Dim stringBuilder As StringBuilder = New StringBuilder()
            Try
                Dim array2 As String() = array
                For i As Integer = 0 To array2.Length - 1
                    Dim text As String = array2(i)
                    If text.Length + stringBuilder.Length <= maxLineLength Then
                        stringBuilder.Append(text + " ")
                    Else
                        If stringBuilder.Length > 0 Then
                            Yield stringBuilder.ToString().Trim()
                            stringBuilder.Clear()
                        End If
                        Dim text2 As String = text
                        While text2.Length > maxLineLength
                            Yield text2.Substring(0, maxLineLength)
                            text2 = text2.Substring(maxLineLength)
                        End While
                        stringBuilder.Append(text2 + " ")
                    End If
                Next
            Finally
            End Try
            Yield stringBuilder.ToString().Trim()
            Return
        End Function


#End Region

#Region "BOM Spreadsheet amendments from Drawing"
        ''' <summary>
        ''' Updates the BOM we already! created with "IsBallooned" Boolean values to help QA.
        ''' </summary>
        ''' <param name="oBOMDoc">the spreadsheet we need to edit</param>
        ''' <remarks></remarks>
        Public Sub UpdateBOMSpreadsheet(ByVal oBOMDoc As String)
            'Dim PartsListForExcel As List(Of BomTool.PartsListRowItem) = New List(Of BomTool.PartsListRowItem)
            Dim PartsListForExcel As List(Of PartsListRowItem) = New List(Of PartsListRowItem)
            Dim oPartsList As PartsList
            Dim oDrawDoc As DrawingDocument = m_inventorApplication.ActiveDocument
            Try
                oPartsList = oDrawDoc.ActiveSheet.PartsLists.Item(1)
            Catch ex As Exception
                MessageBox.Show("No Parts Lists found on the active sheet of this drawing Document, suggest you add one and try again!")
                Exit Sub
            End Try
            Dim excelApp = CreateObject("Excel.Application")
            Dim excelWorkBook As Object
            Dim excelWorkSheet As Object
            excelApp.Visible = True
            excelApp.DisplayAlerts = False

            If Dir(BOMSpreadsheetName) <> "" Then
                'MessageBox.Show("Existing Excel file found for output, updating!")
                excelWorkBook = excelApp.workbooks.Open(BOMSpreadsheetName)
                excelWorkSheet = excelWorkBook.Worksheets(2).Activate
            Else
                MessageBox.Show("File is missing, what happened?")
                Exit Sub
            End If
            For index = 1 To oPartsList.PartsListRows.Count
                Dim oRow As PartsListRow = oPartsList.PartsListRows.Item(index)
                'Dim plRow As BomTool.PartsListRowItem = New BomTool.PartsListRowItem
                Dim plRow As PartsListRowItem = New PartsListRowItem
                plRow.IsBallooned = oRow.Ballooned
                For j = 1 To oPartsList.PartsListColumns.Count
                    Dim oCell As PartsListCell = oRow.Item(j)
                    If oPartsList.PartsListColumns.Item(j).Title = "ITEM" Then
                        If Not oCell.Value = "" Then
                            plRow.ItemNo = oCell.Value
                            PartsListForExcel.Add(plRow)
                            Exit For
                        End If
                    End If
                Next
            Next
            'MessageBox.Show("Parts List for Excel count= " & PartsListForExcel.Count)
            For i = 2 To 200 'maxrowcount
                excelApp.range("A" & i).Select()
                Dim plItem As PartsListRowItem
                Try
                    plItem = (From a As PartsListRowItem In PartsListForExcel
                         Where a.ItemNo = excelApp.ActiveCell.Value
                         Select a).FirstOrDefault()
                Catch ex As Exception
                    Continue For
                End Try
                If Not plItem Is Nothing Then
                    excelApp.Range("L" & i).Select()
                    'MessageBox.Show("Parts List Item= " & plItem.ItemNo)
                    If plItem.IsBallooned Then
                        excelApp.ActiveCell.Value = "�" 'is ballooned
                    Else
                        excelApp.ActiveCell.Value = "�" 'is not ballooned
                    End If
                End If
            Next
            excelWorkBook.Save()
            excelWorkBook.Close()
            excelApp.quit()
            excelWorkSheet = Nothing
            excelWorkBook = Nothing
            excelApp = Nothing
        End Sub
#End Region

        Public Class BomRowItem
            Public Property FileName() As String
                Get
                    Return m_FileName
                End Get
                Set(value As String)
                    m_FileName = value
                End Set
            End Property
            Private m_FileName As String
            Public Property PartNo() As String
                Get
                    Return m_PartNo
                End Get
                Set(value As String)
                    m_PartNo = value
                End Set
            End Property
            Private m_PartNo As String
            Public Property Descr() As String
                Get
                    Return m_Descr
                End Get
                Set(value As String)
                    m_Descr = value
                End Set
            End Property
            Private m_Descr As String
            Public Property Rev() As String
                Get
                    Return m_Rev
                End Get
                Set(value As String)
                    m_Rev = value
                End Set
            End Property
            Private m_Rev As String
            Public Property ItemNo() As Long
                Get
                    Return m_ItemNo
                End Get
                Set(value As Long)
                    m_ItemNo = value
                End Set
            End Property
            Private m_ItemNo As Long
            Public Property Classification() As String
                Get
                    Return m_Classification
                End Get
                Set(value As String)
                    m_Classification = value
                End Set
            End Property
            Private m_Classification As String
            Public Property Material() As String
                Get
                    Return m_Material
                End Get
                Set(value As String)
                    m_Material = value
                End Set
            End Property
            Private m_Material As String
            Public Property Qty() As Long
                Get
                    Return m_Qty
                End Get
                Set(value As Long)
                    m_Qty = value
                End Set
            End Property
            Private m_Qty As Long
            Public Property Vendor() As String
                Get
                    Return m_Vendor
                End Get
                Set(value As String)
                    m_Vendor = value
                End Set
            End Property
            Private m_Vendor As String
            Public Property Comments() As String
                Get
                    Return m_Comments
                End Get
                Set(value As String)
                    m_Comments = value
                End Set
            End Property
            Private m_Comments As String
            Public Property BomRowType() As Long
                Get
                    Return m_BomRowType
                End Get
                Set(value As Long)
                    m_BomRowType = value
                End Set
            End Property
            Private m_BomRowType As Long
            Public Property status() As String
                Get
                    Return m_status
                End Get
                Set(value As String)
                    m_status = value
                End Set
            End Property
            Private m_status As String
        End Class
        Public Class PartsListRowItem
            Implements IComparable(Of PartsListRowItem)
            Public Property ItemNo() As String
                Get
                    Return m_ItemNo
                End Get
                Set(value As String)
                    m_ItemNo = value
                End Set
            End Property
            Private m_ItemNo As String
            Public Property IsBallooned() As [Boolean]
                Get
                    Return m_IsBallooned
                End Get
                Set(value As [Boolean])
                    m_IsBallooned = value
                End Set
            End Property
            Private m_IsBallooned As [Boolean]
            Public Function CompareTo(other As PartsListRowItem) As Integer Implements IComparable(Of PartsListRowItem).CompareTo
                Return Me.CompareTo(other)
            End Function
        End Class

    End Class
    Public Class PartsListRowItem
        Implements IComparable(Of PartsListRowItem)


        Public Function CompareTo(other As PartsListRowItem) As Integer Implements IComparable(Of PartsListRowItem).CompareTo

        End Function
    End Class
End Namespace

