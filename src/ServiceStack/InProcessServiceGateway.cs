﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Web;
using System;

namespace ServiceStack
{
    public class InProcessServiceGateway : IServiceGateway, IServiceGatewayAsync
    {
        private readonly IRequest req;

        public InProcessServiceGateway(IRequest req)
        {
            this.req = req;
        }

        private string SetVerb(object reqeustDto)
        {
            var hold = req.GetItem(Keywords.InvokeVerb) as string;
            if (reqeustDto is IVerb)
            {
                if (reqeustDto is IGet)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Get);
                if (reqeustDto is IPost)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);
                if (reqeustDto is IPut)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Put);
                if (reqeustDto is IDelete)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Delete);
                if (reqeustDto is IPatch)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Patch);
                if (reqeustDto is IOptions)
                    req.SetItem(Keywords.InvokeVerb, HttpMethods.Options);
            }
            return hold;
        }

        private void ResetVerb(string verb)
        {
            if (verb == null)
                req.Items.Remove(Keywords.InvokeVerb);
            else
                req.SetItem(Keywords.InvokeVerb, verb);
        }

        private TResponse ExecSync<TResponse>(object request)
        {
            var response = HostContext.ServiceController.Execute(request, req);
            var responseTask = response as Task;
            if (responseTask != null)
                response = responseTask.GetResult();

            return ConvertToResponse<TResponse>(response);
        }

        private static TResponse ConvertToResponse<TResponse>(object response)
        {
            var error = response as HttpError;
            if (error != null)
                throw error.ToWebServiceException();

            var responseDto = response.GetResponseDto();
            return (TResponse) responseDto;
        }

        private Task<TResponse> ExecAsync<TResponse>(object request)
        {
            var responseTask = HostContext.ServiceController.ExecuteAsync(request, req, applyFilters:false);
            return responseTask.ContinueWith(task => ConvertToResponse<TResponse>(task.Result));
        }

        public TResponse Send<TResponse>(object requestDto)
        {
            var holdDto = req.Dto;
            var holdVerb = SetVerb(requestDto);
            try
            {
                return ExecSync<TResponse>(requestDto);
            }
            finally
            {
                req.Dto = holdDto;
                ResetVerb(holdVerb);
            }
        }

        public Task<TResponse> SendAsync<TResponse>(object requestDto, CancellationToken token = new CancellationToken())
        {
            var holdDto = req.Dto;
            var holdVerb = SetVerb(requestDto);

            return ExecAsync<TResponse>(requestDto)
                .ContinueWith(task => {
                    req.Dto = holdDto;
                    ResetVerb(holdVerb);
                    return task.Result;
                }, token);
        }

        private static object[] CreateTypedArray(IEnumerable<object> requestDtos)
        {
            var requestsArray = requestDtos.ToArray();
            var elType = requestDtos.GetType().GetCollectionType();
            var toArray = (object[])Array.CreateInstance(elType, requestsArray.Length);
            for (int i = 0; i < requestsArray.Length; i++)
            {
                toArray[i] = requestsArray[i];
            }
            return toArray;
        }

        public List<TResponse> SendAll<TResponse>(IEnumerable<object> requestDtos)
        {
            var holdDto = req.Dto;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;
            var typedArray = CreateTypedArray(requestDtos);
            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);

            try
            {
                return ExecSync<TResponse[]>(typedArray).ToList();
            }
            finally
            {
                req.Dto = holdDto;
                ResetVerb(holdVerb);
            }
        }

        public Task<List<TResponse>> SendAllAsync<TResponse>(IEnumerable<object> requestDtos, CancellationToken token = new CancellationToken())
        {
            var holdDto = req.Dto;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;
            var typedArray = CreateTypedArray(requestDtos);
            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);

            return ExecAsync<TResponse[]>(typedArray)
                .ContinueWith(task => 
                {
                    req.Dto = holdDto;
                    ResetVerb(holdVerb);
                    return task.Result.ToList();
                }, token);
        }

        public void Publish(object requestDto)
        {
            var holdDto = req.Dto;
            var holdAttrs = req.RequestAttributes;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;

            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);
            req.RequestAttributes &= ~RequestAttributes.Reply;
            req.RequestAttributes |= RequestAttributes.OneWay;

            try
            {
                var response = HostContext.ServiceController.Execute(requestDto, req);
            }
            finally
            {
                req.Dto = holdDto;
                req.RequestAttributes = holdAttrs;
                ResetVerb(holdVerb);
            }
        }

        public Task PublishAsync(object requestDto, CancellationToken token = new CancellationToken())
        {
            var holdDto = req.Dto;
            var holdAttrs = req.RequestAttributes;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;

            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);
            req.RequestAttributes &= ~RequestAttributes.Reply;
            req.RequestAttributes |= RequestAttributes.OneWay;

            return HostContext.ServiceController.ExecuteAsync(requestDto, req, applyFilters: false)
                .ContinueWith(task => 
                {
                    req.Dto = holdDto;
                    req.RequestAttributes = holdAttrs;
                    ResetVerb(holdVerb);
                }, token);
        }

        public void PublishAll(IEnumerable<object> requestDtos)
        {
            var holdDto = req.Dto;
            var holdAttrs = req.RequestAttributes;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;

            var typedArray = CreateTypedArray(requestDtos);
            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);
            req.RequestAttributes &= ~RequestAttributes.Reply;
            req.RequestAttributes |= RequestAttributes.OneWay;

            try
            {
                var response = HostContext.ServiceController.Execute(typedArray, req);
            }
            finally
            {
                req.Dto = holdDto;
                req.RequestAttributes = holdAttrs;
                ResetVerb(holdVerb);
            }
        }

        public Task PublishAllAsync(IEnumerable<object> requestDtos, CancellationToken token = new CancellationToken())
        {
            var holdDto = req.Dto;
            var holdAttrs = req.RequestAttributes;
            string holdVerb = req.GetItem(Keywords.InvokeVerb) as string;

            var typedArray = CreateTypedArray(requestDtos);
            req.SetItem(Keywords.InvokeVerb, HttpMethods.Post);
            req.RequestAttributes &= ~RequestAttributes.Reply;
            req.RequestAttributes |= RequestAttributes.OneWay;

            return HostContext.ServiceController.ExecuteAsync(typedArray, req, applyFilters: false)
                .ContinueWith(task =>
                {
                    req.Dto = holdDto;
                    req.RequestAttributes = holdAttrs;
                    ResetVerb(holdVerb);
                }, token);
        }
    }

    public abstract class ServiceGatewayFactoryBase : IServiceGatewayFactory, IServiceGateway, IServiceGatewayAsync
    {
        protected InProcessServiceGateway localGateway;

        public virtual IServiceGateway GetServiceGateway(IRequest request)
        {
            localGateway = new InProcessServiceGateway(request);
            return this;
        }

        public abstract IServiceGateway GetGateway(Type requestType);

        protected virtual IServiceGatewayAsync GetGatewayAsync(Type requestType)
        {
            return (IServiceGatewayAsync)GetGateway(requestType);
        }

        public TResponse Send<TResponse>(object requestDto)
        {
            return GetGateway(requestDto.GetType()).Send<TResponse>(requestDto);
        }

        public List<TResponse> SendAll<TResponse>(IEnumerable<object> requestDtos)
        {
            return GetGateway(requestDtos.GetType().GetCollectionType()).SendAll<TResponse>(requestDtos);
        }

        public void Publish(object requestDto)
        {
            GetGateway(requestDto.GetType()).Publish(requestDto);
        }

        public void PublishAll(IEnumerable<object> requestDtos)
        {
            GetGateway(requestDtos.GetType().GetCollectionType()).PublishAll(requestDtos);
        }

        public Task<TResponse> SendAsync<TResponse>(object requestDto, CancellationToken token = new CancellationToken())
        {
            return GetGatewayAsync(requestDto.GetType()).SendAsync<TResponse>(requestDto, token);
        }

        public Task<List<TResponse>> SendAllAsync<TResponse>(IEnumerable<object> requestDtos, CancellationToken token = new CancellationToken())
        {
            return GetGatewayAsync(requestDtos.GetType().GetCollectionType()).SendAllAsync<TResponse>(requestDtos, token);
        }

        public Task PublishAsync(object requestDto, CancellationToken token = new CancellationToken())
        {
            return GetGatewayAsync(requestDto.GetType()).PublishAsync(requestDto, token);
        }

        public Task PublishAllAsync(IEnumerable<object> requestDtos, CancellationToken token = new CancellationToken())
        {
            return GetGatewayAsync(requestDtos.GetType().GetCollectionType()).PublishAllAsync(requestDtos, token);
        }
    }
}