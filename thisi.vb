Sub AddThickBorderOnValueChange()
    Dim ws As Worksheet
    Dim rng As Range
    Dim cell As Range
    Dim lastRow As Long
    
    ' Set reference to active worksheet
    Set ws = ActiveSheet
    
    ' Find last row in column A
    lastRow = ws.Cells(ws.Rows.Count, "A").End(xlUp).Row
    
    ' Set range to column A, excluding the last cell
    Set rng = ws.Range("A1:A" & lastRow)
    
    ' Iterate through cells in column A
    For Each cell In rng
        ' Check if current cell is not the last cell in range
        If cell.Row < lastRow Then
            ' Compare current cell with next cell
            If cell.Value <> cell.Offset(1, 0).Value Then
                ' Add thick bottom border to entire row
                cell.EntireRow.Borders(xlBottom).LineStyle = xlContinuous
                cell.EntireRow.Borders(xlBottom).Weight = xlThick
            End If
        End If
    Next cell
End Sub