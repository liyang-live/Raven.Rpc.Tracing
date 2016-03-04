﻿using Raven.Rpc.IContractModel;
using Raven.Rpc.Tracing;
using Raven.Rpc.Tracing.Record;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Raven.Rpc.HttpProtocol.Tracing
{

    public static class RpcHttpClientExtension
    {
        private static ITracingRecord record = ServiceContainer.Resolve<ITracingRecord>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public static void RegistTracing(this RpcHttpClient client)
        {
            client.RequestContentDataHandler += Client_RequestContentDataHandler;
            client.OnRequest += Client_OnRequest;
            client.OnResponse += Client_OnResponse;
            client.OnError += Client_OnError;
        }

        private static object Client_RequestContentDataHandler(object data)
        {
            var reqModel = data as IRequestModel<Header>;
            if (reqModel != null)
            {
                if (reqModel.Header == null)
                {
                    reqModel.Header = new Header();
                }

                var modelHeader = HttpContentData.GetRequestHeader();
                reqModel.Header.RpcID = Util.VersionIncr(HttpContentData.GetSubRpcID());
                HttpContentData.SetSubRpcID(reqModel.Header.RpcID);
                reqModel.Header.TrackID = modelHeader.TrackID;
                reqModel.Header.UUID = modelHeader.UUID;
            }

            return data;
        }

        private static void Client_OnRequest(System.Net.Http.HttpRequestMessage request)
        {
        }

        private static void Client_OnResponse(HttpResponseMessage response, RpcContext rpcContext)
        {
            ClientSR sr = new ClientSR();
            sr.IsSuccess = true;
            sr.IsException = false;
            FillClientSR(sr,response.RequestMessage, rpcContext);

            Record(sr);
        }

        private static void Client_OnError(Exception ex, HttpRequestMessage request, RpcContext rpcContext)
        {
            ClientSR sr = new ClientSR();
            sr.IsSuccess = false;
            sr.IsException = true;
            FillClientSR(sr, request, rpcContext);

            sr.Extension.Add("Exception", Util.GetFullExceptionMessage(ex));

            Record(sr);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sr"></param>
        /// <param name="request"></param>
        /// <param name="rpcContext"></param>
        private static void FillClientSR(ClientSR sr, HttpRequestMessage request, RpcContext rpcContext)
        {
            var modelHeader = HttpContentData.GetRequestHeader();
            var uri = request.RequestUri;
            sr.ServiceMethod = uri.AbsoluteUri;
            sr.Extension.Add(nameof(uri.AbsolutePath), uri.AbsolutePath);
            sr.Extension.Add(nameof(uri.PathAndQuery), uri.PathAndQuery);
            sr.Extension.Add(nameof(uri.Host), uri.Host);

            sr.Extension.Add(nameof(rpcContext.RequestModel), rpcContext.RequestModel);
            sr.Extension.Add(nameof(rpcContext.ResponseModel), rpcContext.ResponseModel);
            sr.Extension.Add(nameof(rpcContext.ResponseSize), rpcContext.ResponseSize);

            sr.Protocol = uri.Scheme;

            sr.SendSTime = rpcContext.SendStartTime;
            sr.ReceiveETime = rpcContext.ReceiveEndTime;
            sr.TimeLength = sr.ReceiveETime.HasValue ? (sr.ReceiveETime.Value - sr.SendSTime).TotalMilliseconds : 0D;
            sr.RpcId = modelHeader.RpcID;
            sr.TraceId = modelHeader.TrackID;            

            if (rpcContext.ResponseModel != null)
            {
                var reqModel = rpcContext.ResponseModel as IResponseModel;
                if (reqModel != null)
                {
                    sr.Code = reqModel.GetCode();
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sr"></param>
        private static void Record(ClientSR sr)
        {
            record.RecordClientSR(sr);
        }

    }
}
