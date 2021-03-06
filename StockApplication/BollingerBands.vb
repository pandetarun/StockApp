﻿Imports FirebirdSql.Data.FirebirdClient
Imports System.Collections.Generic

Public Class BollingerBands

    Dim lastTradedPrice, BBUper, BBLower, simpleMA As Double
    Dim PeriodBandwidth As Double
    Dim MATime, MAStock As String
    Dim MADate As Date
    Dim counter As Integer

    Public Function CalculateAndStoreIntradayBollingerBands() As Boolean
        Dim tmpStockList As List(Of String) = New List(Of String)
        Dim tmpStockCode As String
        Dim ds As FbDataReader = Nothing

        StockAppLogger.Log("CalculateAndStoreIntradayBollingerBands Start", "BollingerBands")
        Try
            ds = DBFunctions.getDataFromTableExt("NSE_INDICES_TO_STOCK_MAPPING", "CI")
            While ds.Read()
                tmpStockCode = ds.GetValue(ds.GetOrdinal("STOCK_NAME"))
                If Not tmpStockList.Contains(tmpStockCode) Then
                    tmpStockList.Add(tmpStockCode)
                    If IntraDayBBCalculation(tmpStockCode) Then
                        StockAppLogger.LogInfo("CalculateAndStoreIntradayBollingerBands BollingerBands stored for Stock = " & tmpStockCode & " at time = " & MATime, "BollingerBands")
                    Else
                        StockAppLogger.LogInfo("CalculateAndStoreIntradayBollingerBands GetStockListAndCalculateBollingerBands BollingerBands failed for Stock = " & tmpStockCode & " at time = " & MATime, "BollingerBands")
                    End If
                End If
            End While
            'DBFunctions.CloseSQLConnectionExt("CI")
        Catch exc As Exception
            StockAppLogger.LogError(" CalculateAndStoreIntradayBollingerBandsError Occurred in getting stocklist from DB = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("CalculateAndStoreIntradayBollingerBands End", "BollingerBands")
        Return True
    End Function

    Public Function IntraDayBBCalculation(tmpStockCode As String) As Boolean

        Dim ds As FbDataReader = Nothing
        Dim ds1 As FbDataReader = Nothing
        Dim whereClause As String
        Dim orderClause As String
        Dim closingPrice As Double
        Dim tradedPrice, totalTradedPrice As Double
        Dim perioddeviation As Double
        Dim PeriodData As List(Of Double) = New List(Of Double)
        Dim configuredBBPeriods As String
        Dim tmpBBPeriods As List(Of String)
        Dim tmpPeriodData As List(Of Double)
        Dim totalRecords As Integer = 0

        StockAppLogger.Log("IntraDayBBCalculation Start", "BollingerBands")
        tmpBBPeriods = Nothing
        lastTradedPrice = 0
        counter = 1
        totalTradedPrice = 0
        simpleMA = 0
        BBUper = 0
        BBLower = 0
        MAStock = tmpStockCode
        Try
            whereClause = "LASTUPDATEDATE='" & Today & "' and companycode = '" & tmpStockCode & "'"
            orderClause = "lastupdatetime desc"
            MADate = Today
            ds = DBFunctions.getDataFromTableExt("STOCKHOURLYDATA", "CI", " count(lastClosingPrice) totalRows", whereClause)
            If ds.Read() Then
                totalRecords = ds.GetValue(ds.GetOrdinal("totalRows"))
            End If
            ds.Close()
            ds = DBFunctions.getDataFromTableExt("STOCKHOURLYDATA", "CI", " lastClosingPrice, lastupdatetime", whereClause, orderClause)
            ds1 = DBFunctions.getDataFromTableExt("STOCKWISEPERIODS", "CI", " INTRADAYBBPERIOD", "stockname = '" & tmpStockCode & "'")
            If ds1.Read() Then
                configuredBBPeriods = ds1.GetValue(ds1.GetOrdinal("INTRADAYBBPERIOD"))
                tmpBBPeriods = New List(Of String)(configuredBBPeriods.Split(","))
            End If
            ds1.Close()
            If totalRecords > tmpBBPeriods.Item(0) Then
                While ds.Read()

                    If counter <= tmpBBPeriods.Item(tmpBBPeriods.Count() - 1) Then
                        If counter = 1 Then
                            MATime = ds.GetValue(ds.GetOrdinal("lastupdatetime"))
                            closingPrice = ds.GetValue(ds.GetOrdinal("lastClosingPrice"))
                            lastTradedPrice = closingPrice
                        End If
                        tradedPrice = Double.Parse(ds.GetValue(ds.GetOrdinal("lastClosingPrice")))
                        totalTradedPrice = totalTradedPrice + tradedPrice
                        PeriodData.Add(tradedPrice)
                        If tmpBBPeriods.Contains(counter) Then
                            perioddeviation = 0
                            'BBLower = 0
                            'BBUper = 0
                            tmpPeriodData = New List(Of Double)
                            simpleMA = totalTradedPrice / counter
                            For counter1 As Integer = 0 To counter - 1
                                tmpPeriodData.Add(PeriodData.Item(counter1) - simpleMA)
                                tmpPeriodData.Item(counter1) = tmpPeriodData.Item(counter1) * tmpPeriodData.Item(counter1)
                                perioddeviation = perioddeviation + tmpPeriodData.Item(counter1)
                            Next counter1
                            perioddeviation = perioddeviation / counter
                            perioddeviation = Math.Sqrt(perioddeviation)
                            BBLower = simpleMA - 2 * perioddeviation
                            BBUper = simpleMA + 2 * perioddeviation
                            PeriodBandwidth = BBUper - BBLower
                            InsertIntraDayBBtoDB(counter)
                            If tmpBBPeriods.IndexOf(counter) = tmpBBPeriods.Count Then
                                Exit While
                            End If
                        End If
                    Else
                        Exit While
                    End If
                    counter = counter + 1
                End While
            End If
            ds.Close()
        Catch exc As Exception
            StockAppLogger.LogError("IntraDayBBCalculation Error Occurred in calculating intraday Bollinger Band = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("IntraDayBBCalculation End", "BollingerBands")
        Return True
    End Function

    Private Function InsertIntraDayBBtoDB(ByVal period As Integer) As Boolean

        Dim insertStatement As String
        Dim insertValues As String
        Dim sqlStatement As String
        Dim fireQuery As Boolean = False

        StockAppLogger.Log("InsertIntraDayBBtoDB Start", "BollingerBands")
        Try
            insertStatement = "INSERT INTO INTRADAYBOLLINGERBANDS (TRADEDDATE, STOCKNAME, LASTUPDATETIME, PERIOD, CLOSINGPRICE, SMA, UPPERBAND, LOWERBAND, BANDWIDTH) "
            insertValues = "VALUES ('" & MADate & "', '" & MAStock & "', '" & MATime & "', " & period & ", " & lastTradedPrice & ", " & simpleMA & ", " & BBUper & ", " & BBLower & ", " & PeriodBandwidth & ");"

            sqlStatement = insertStatement & insertValues
            DBFunctions.ExecuteSQLStmtExt(sqlStatement, "CI")
        Catch exc As Exception
            StockAppLogger.LogError("InsertIntraDayBBtoDB Error Occurred in storing intraday bollinger band = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("InsertIntraDayBBtoDB End", "BollingerBands")
        Return True
    End Function

    Public Function CalculateAndStoredailyBollingerBands() As Boolean
        Dim tmpStockList As List(Of String) = New List(Of String)
        Dim tmpStockCode As String
        Dim ds As FbDataReader = Nothing

        StockAppLogger.Log("CalculateAndStoredailyBollingerBands Start", "BollingerBands")
        Try
            ds = DBFunctions.getDataFromTableExt("NSE_INDICES_TO_STOCK_MAPPING", "CI")
            While ds.Read()
                tmpStockCode = ds.GetValue(ds.GetOrdinal("STOCK_NAME"))
                If Not tmpStockList.Contains(tmpStockCode) Then
                    tmpStockList.Add(tmpStockCode)
                    If DailyBBCalculation(tmpStockCode) Then
                        StockAppLogger.LogInfo("CalculateAndStoredailyBollingerBands BollingerBands stored for Stock = " & tmpStockCode & " at time = " & MATime, "BollingerBands")
                    Else
                        StockAppLogger.LogInfo("CalculateAndStoredailyBollingerBands BollingerBands failed for Stock = " & tmpStockCode & " at time = " & MATime, "BollingerBands")
                    End If
                End If
            End While
            DBFunctions.CloseSQLConnectionExt("CI")
        Catch exc As Exception
            StockAppLogger.LogError("CalculateAndStoredailyBollingerBands Error Occurred in getting stocklist from DB = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("CalculateAndStoredailyBollingerBands End", "BollingerBands")
        Return True
    End Function

    Public Function DailyBBCalculation(tmpStockCode As String) As Boolean

        Dim ds As FbDataReader = Nothing
        Dim ds1 As FbDataReader = Nothing
        Dim whereClause As String
        Dim orderClause As String
        Dim configuredBBPeriods As String
        Dim tmpBBPeriods As List(Of String)
        Dim closingPrice As Double
        Dim tradedPrice, totalTradedPrice As Double
        Dim perioddeviation As Double
        Dim PeriodData As List(Of Double) = New List(Of Double)

        Dim tmpPeriodData As List(Of Double)

        StockAppLogger.Log("DailyBBCalculation Start", "BollingerBands")

        tmpBBPeriods = Nothing
        lastTradedPrice = 0
        counter = 0
        totalTradedPrice = 0
        simpleMA = 0
        BBUper = 0
        BBLower = 0
        MAStock = tmpStockCode
        Try
            whereClause = "TRADEDDATE='" & Today & "' and STOCKNAME = '" & tmpStockCode & "'"
            orderClause = "TRADEDDATE desc"
            MADate = Today
            ds = DBFunctions.getDataFromTableExt("DAILYSTOCKDATA", "CI", " last_traded_price", whereClause, orderClause)
            ds1 = DBFunctions.getDataFromTableExt("STOCKWISEPERIODS", "CI", " DAILYBBPERIOD", "stockname = '" & tmpStockCode & "'")
            If ds1.Read() Then
                configuredBBPeriods = ds1.GetValue(ds1.GetOrdinal("DAILYBBPERIOD"))
                tmpBBPeriods = New List(Of String)(configuredBBPeriods.Split(","))
            End If
            ds1.Close()
            While ds.Read()
                If counter = 0 Then
                    closingPrice = ds.GetValue(ds.GetOrdinal("last_traded_price"))
                    lastTradedPrice = closingPrice
                End If
                tradedPrice = Double.Parse(ds.GetValue(ds.GetOrdinal("last_traded_price")))
                totalTradedPrice = totalTradedPrice + tradedPrice
                PeriodData.Add(tradedPrice)
                If tmpBBPeriods.Contains(counter) Then
                    perioddeviation = 0
                    BBLower = 0
                    BBUper = 0
                    tmpPeriodData = New List(Of Double)
                    simpleMA = totalTradedPrice / counter
                    For counter1 As Integer = 0 To counter
                        tmpPeriodData.Item(counter1) = PeriodData.Item(counter1) - simpleMA
                        tmpPeriodData.Item(counter1) = tmpPeriodData.Item(counter1) * tmpPeriodData.Item(counter1)
                        perioddeviation = perioddeviation + tmpPeriodData.Item(counter1)
                    Next counter1
                    perioddeviation = perioddeviation / counter
                    perioddeviation = Math.Sqrt(perioddeviation)
                    BBLower = simpleMA - 2 * perioddeviation
                    BBUper = simpleMA + 2 * perioddeviation
                    PeriodBandwidth = BBUper - BBLower
                    InsertIntraDayBBtoDB(counter)
                    If tmpBBPeriods.IndexOf(counter) = tmpBBPeriods.Count - 1 Then
                        Exit While
                    End If
                End If
                counter = counter + 1
            End While
            ds.Close()
        Catch exc As Exception
            StockAppLogger.LogError("DailyBBCalculation Error Occurred in calculating daily Bollinger Band = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("DailyBBCalculation End", "BollingerBands")
        Return True
    End Function

    Private Function InsertDailyBBtoDB(ByVal period As Integer) As Boolean

        Dim insertStatement As String
        Dim insertValues As String
        Dim sqlStatement As String
        Dim fireQuery As Boolean = False

        StockAppLogger.Log("InsertDailyBBtoDB Start", "BollingerBands")
        Try
            insertStatement = "INSERT INTO DAILYBOLLINGERBANDS (TRADEDDATE, STOCKNAME, PERIOD, CLOSINGPRICE, SMA, UPPERBAND, LOWERBAND, BANDWIDTH) "
            insertValues = "VALUES ('" & MADate & "', '" & MAStock & "', ', " & period & ", " & lastTradedPrice & ", " & simpleMA & ", " & BBUper & ", " & BBLower & ", " & PeriodBandwidth & ");"

            sqlStatement = insertStatement & insertValues
            DBFunctions.ExecuteSQLStmtExt(sqlStatement, "CI")
        Catch exc As Exception
            StockAppLogger.LogError("InsertDailyBBtoDB Error Occurred in storing intraday bollinger band = ", exc, "BollingerBands")
            Return False
        End Try
        StockAppLogger.Log("InsertDailyBBtoDB End", "BollingerBands")
        Return True
    End Function

End Class

