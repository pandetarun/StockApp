﻿Imports System.Net
Public Class Helper
    Public Function getStockList() As String()

    End Function

    Public Shared Function GetDataFromUrl(ByVal urlToGet As String) As String
        Dim serverUrl As String
        Dim request As HttpWebRequest
        Dim response As HttpWebResponse
        Dim strResponse As String

        'stockCode = "INFY"
        serverUrl = urlToGet
        ' "http://www.nseindia.com/live_market/dynaContent/live_watch/get_quote/ajaxGetQuoteJSON.jsp?symbol=" & stockCode
        request = WebRequest.Create(serverUrl)
        request = CreateRequest(request)
        response = request.GetResponse()
        Dim sReader As New IO.StreamReader(response.GetResponseStream)
        strResponse = sReader.ReadToEnd()
        response.Close()
        Return strResponse
    End Function

    Private Shared Function CreateRequest(ByRef request As HttpWebRequest) As HttpWebRequest
        request.UseDefaultCredentials = True
        request.Method = WebRequestMethods.Http.Post
        request.ContentType = "application/json"
        With request
            .UserAgent = "User-Agent: Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.8.0.10) Gecko/20070216 Firefox/1.5.0.10"
            .KeepAlive = False
            .Method = "GET"
            .AllowAutoRedirect = True
            .Timeout = 60 * 1000
            .Headers.Add("Pragma", "no-cache")
            .Headers.Add("Cache-Control", "no-cache")
            .Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
            .ServicePoint.Expect100Continue = False
        End With
        Return request
    End Function
End Class