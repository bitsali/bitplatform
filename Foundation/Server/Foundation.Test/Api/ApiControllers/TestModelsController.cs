﻿using System;
using System.Linq;
using System.Threading;
using System.Web.Http;
using Foundation.Api.ApiControllers;
using Foundation.Test.Model.DomainModels;
using System.Threading.Tasks;
using System.Web.OData;
using Foundation.Api.Contracts;
using Foundation.Core.Contracts;
using Foundation.Test.Core.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Foundation.Api.Exceptions;
using System.Net.Http;
using Foundation.DataAccess.Contracts;
using Microsoft.Owin;

namespace Foundation.Test.Api.ApiControllers
{
    public class TestModelsController : DtoController<TestModel>
    {
        private readonly Lazy<IRepository<TestModel>> _testModelsRepository;
        private readonly Lazy<IBackgroundJobWorker> _backgroundJobWorker;
        private readonly Lazy<IMessageSender> _messageSender;
        private readonly Lazy<IDateTimeProvider> _dateTimeProvider;

        public TestModelsController(Lazy<IRepository<TestModel>> testModelsRepository, Lazy<IBackgroundJobWorker> backgroundJobWorker, Lazy<IMessageSender> messageSender, Lazy<IDateTimeProvider> dateTimeProvider)
        {
            if (testModelsRepository == null)
                throw new ArgumentNullException(nameof(testModelsRepository));

            _testModelsRepository = testModelsRepository;

            if (backgroundJobWorker == null)
                throw new ArgumentNullException(nameof(backgroundJobWorker));

            _backgroundJobWorker = backgroundJobWorker;

            if (messageSender == null)
                throw new ArgumentNullException(nameof(messageSender));

            _messageSender = messageSender;

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        protected TestModelsController()
        {

        }

        [Get]
        [AllowAnonymous]
        public virtual async Task<IQueryable<TestModel>> Get(CancellationToken cancellationToken)
        {
            return await _testModelsRepository.Value
                .GetAllAsync(cancellationToken);
        }

        [Get]
        public virtual async Task<TestModel> Get(long key, CancellationToken cancellationToken)
        {
            TestModel testModel = await (await _testModelsRepository.Value
                .GetAllAsync(cancellationToken))
                .FirstOrDefaultAsync(t => t.Id == key, cancellationToken);

            if (testModel == null)
                throw new ResourceNotFoundException();

            return testModel;
        }

        [Create]
        public virtual async Task<TestModel> Create(TestModel model, CancellationToken cancellationToken)
        {
            model = await _testModelsRepository.Value.AddAsync(model, cancellationToken);

            return model;
        }

        [PartialUpdate]
        public virtual async Task<TestModel> PartialUpdate(long key, Delta<TestModel> modelDelta,
            CancellationToken cancellationToken)
        {
            TestModel model = await (await _testModelsRepository.Value.GetAllAsync(cancellationToken))
                .FirstOrDefaultAsync(m => m.Id == key, cancellationToken);

            if (model == null)
                throw new ResourceNotFoundException();

            modelDelta.Patch(model);

            model = await _testModelsRepository.Value.UpdateAsync(model, cancellationToken);

            return model;
        }

        [Update]
        public virtual async Task<TestModel> Update(long key, TestModel model,
            CancellationToken cancellationToken)
        {
            model = await _testModelsRepository.Value.UpdateAsync(model, cancellationToken);

            if (model.Id != key)
                throw new BadRequestException();

            return model;
        }

        [Delete]
        public virtual async Task Delete(long key, CancellationToken cancellationToken)
        {
            TestModel model = await (await _testModelsRepository.Value.GetAllAsync(cancellationToken))
                .FirstOrDefaultAsync(m => m.Id == key, cancellationToken);

            if (model == null)
                throw new ResourceNotFoundException();

            await _testModelsRepository.Value.DeleteAsync(model, cancellationToken);
        }

        public class EmailParameters
        {
            public string title { get; set; }

            public string message { get; set; }

            public string to { get; set; }
        }

        [Action]
        [Parameter("title", typeof(string))]
        [Parameter("message", typeof(string))]
        [Parameter("to", typeof(string))]
        public virtual async Task<Guid> SendEmailUsingBackgroundJobService(EmailParameters actionParameters)
        {
            string title = actionParameters.title;
            string message = actionParameters.message;
            string to = actionParameters.to;

            string jobId = await _backgroundJobWorker.Value
                .PerformBackgroundJobAsync<IEmailService>(emailService => emailService.SendEmail(to, title, message));

            return Guid.Parse(jobId);
        }

        [Action]
        [Parameter("title", typeof(string))]
        [Parameter("message", typeof(string))]
        [Parameter("to", typeof(string))]
        public virtual async Task<Guid> SendEmailUsingBackgroundJobServiceAndPushAfterThat(EmailParameters actionParameters)
        {
            string title = actionParameters.title;
            string message = actionParameters.message;
            string to = actionParameters.to;

            string jobId = await _backgroundJobWorker.Value
                .PerformBackgroundJobAsync<IEmailService>(emailService => emailService.SendEmail(to, title, message));

            string secondJobId = await _backgroundJobWorker.Value
                .PerformBackgroundJobWhenAnotherJobSucceededAsync<IMessageSender>(jobId,
                    messageSender => messageSender.SendMessageToUsers("OnEmailSent", new { Title = title }, to));

            return Guid.Parse(secondJobId);
        }

        [Action]
        [Parameter("title", typeof(string))]
        [Parameter("message", typeof(string))]
        [Parameter("to", typeof(string))]
        public virtual void SendEmail(EmailParameters actionParameters)
        {
            string title = actionParameters.title;
            string message = actionParameters.message;
            string to = actionParameters.to;

            Request.GetOwinContext().GetDependencyResolver().Resolve<IEmailService>().SendEmail(to, title, message);
        }

        [Action]
        public virtual async Task PushSomethingWithDateTimeOffset()
        {
            await _messageSender.Value.SendMessageToUsersAsync("TestTask", new { Date = _dateTimeProvider.Value.GetCurrentUtcDateTime() }, "SomeUser");
        }

        public class WordParameters
        {
            public string to { get; set; }

            public string word { get; set; }
        }

        [Action]
        [Parameter("to", typeof(string))]
        [Parameter("word", typeof(string))]
        public virtual async Task PushSomeWordToAnotherUser(WordParameters parameters, CancellationToken cancellationToken)
        {
            string to = parameters.to;
            string word = parameters.word;

            await _messageSender.Value.SendMessageToUsersAsync("NewWord", new { Word = word }, to);
        }

        [Action]
        [Parameter("to", typeof(string))]
        [Parameter("word", typeof(string))]
        public virtual async Task PushSomeWordToAnotherUsingBackgroundJobWorker(WordParameters parameters, CancellationToken cancellationToken)
        {
            string to = parameters.to;
            string word = parameters.word;

            await _backgroundJobWorker.Value.PerformBackgroundJobAsync<IMessageSender>(messageSender =>
                        messageSender.SendMessageToUsers("NewWord", new { Word = word }, to));
        }

        public class StringFormattersTestsParameters
        {
            public string simpleString { get; set; }

            public IEnumerable<string> stringsArray { get; set; }

            public IEnumerable<string> stringsArray2 { get; set; }

            public TestModel simpleDto { get; set; }

            public IEnumerable<TestModel> entitiesArray { get; set; }
        }

        [Action]
        [Parameter("simpleString", typeof(string))]
        [Parameter("stringsArray", typeof(IEnumerable<string>))]
        [Parameter("stringsArray2", typeof(IEnumerable<string>))]
        [Parameter("simpleDto", typeof(TestModel))]
        [Parameter("entitiesArray", typeof(IEnumerable<TestModel>))]
        public virtual void StringFormattersTests(StringFormattersTestsParameters actionParameters)
        {
            string simpleString = actionParameters.simpleString;
            List<string> stringsArray = actionParameters.stringsArray.ToList();
            TestModel simpleDto = actionParameters.simpleDto;
            List<TestModel> entitiesArray = actionParameters.entitiesArray.ToList();
            IValueChecker valueChecker = Request.GetOwinContext().GetDependencyResolver().Resolve<IValueChecker>();
            valueChecker.CheckValue(simpleString);
            valueChecker.CheckValue(stringsArray);
            valueChecker.CheckValue(simpleDto);
            valueChecker.CheckValue(entitiesArray);
        }

        public class TimeZoneTestsParameters
        {
            public DateTimeOffset simpleDate { get; set; }

            public IEnumerable<DateTimeOffset> datesArray { get; set; }

            public TestModel simpleDto { get; set; }

            public IEnumerable<TestModel> entitiesArray { get; set; }
        }

        [Action]
        [Parameter("simpleDate", typeof(DateTimeOffset))]
        [Parameter("datesArray", typeof(IEnumerable<DateTimeOffset>))]
        [Parameter("simpleDto", typeof(TestModel))]
        [Parameter("entitiesArray", typeof(IEnumerable<TestModel>))]
        public virtual void TimeZoneTests(TimeZoneTestsParameters actionParameters)
        {
            DateTimeOffset simpleDate = actionParameters.simpleDate;
            List<DateTimeOffset> datesArray = actionParameters.datesArray.ToList();
            TestModel simpleDto = actionParameters.simpleDto;
            List<TestModel> entitiesArray = actionParameters.entitiesArray.ToList();
            IValueChecker valueChecker = Request.GetOwinContext().GetDependencyResolver().Resolve<IValueChecker>();
            valueChecker.CheckValue(simpleDate);
            valueChecker.CheckValue(datesArray);
            valueChecker.CheckValue(simpleDto);
            valueChecker.CheckValue(entitiesArray);
        }

        [Function]
        public virtual async Task<TestModel> CustomActionMethodWithSingleDtoReturnValueTest(CancellationToken cancellationToken)
        {
            return await (await _testModelsRepository.Value
                .GetAllAsync(cancellationToken))
                .FirstAsync(cancellationToken);
        }

        [Function]
        public virtual async Task<IEnumerable<TestModel>> CustomActionMethodWithArrayOfEntitiesReturnValueTest(CancellationToken cancellationToken)
        {
            return await (await _testModelsRepository.Value
                .GetAllAsync(cancellationToken))
                .ToListAsync(cancellationToken);
        }

        [Function]
        public virtual async Task<IQueryable<TestModel>> CustomActionMethodWithQueryableOfEntitiesReturnValueTest(CancellationToken cancellationToken)
        {
            return await _testModelsRepository.Value
                .GetAllAsync(cancellationToken);
        }

        [Function]
        [Parameter("val", typeof(long))]
        public virtual TestModel[] GetTestModelsByStringPropertyValue(long val)
        {
            return new[]
            {
                new TestModel { Id = 1, DateProperty = DateTimeOffset.Now, StringProperty = "String1", Version = 1 },
                new TestModel { Id = 2, DateProperty = DateTimeOffset.Now, StringProperty = "String2", Version = 2 }
            };
        }

        public class FirstSecondParameters
        {
            public int firstValue { get; set; }

            public int secondValue { get; set; }
        }

        [Action]
        [Parameter("firstValue", typeof(int))]
        [Parameter("secondValue", typeof(int))]
        public virtual bool AreEqual(FirstSecondParameters parameters)
        {
            return parameters.firstValue == parameters.secondValue;
        }

        [Action]
        [Parameter("firstValue", typeof(int))]
        [Parameter("secondValue", typeof(int))]
        public virtual int Sum(FirstSecondParameters parameters)
        {
            return parameters.firstValue + parameters.secondValue;
        }

        [Function]
        public virtual TestModel[] GetSomeTestModelsForTest()
        {
            return new[] {
                new TestModel
                {
                    StringProperty = "Test",
                    DateProperty = new DateTimeOffset(2016, 1, 1, 10, 30, 0, TimeSpan.Zero),
                    Version = 1
                }
            };
        }

        public class TestIEEE754CompatibilityParameters
        {
            public decimal val { get; set; }
        }

        [Action]
        [Parameter("val", typeof(decimal))]
        public virtual decimal TestIEEE754Compatibility(TestIEEE754CompatibilityParameters parameters)
        {
            decimal val = parameters.val;
            return val;
        }

        public class TestIEEE754Compatibility2Parameters
        {
            public int val { get; set; }
        }

        [Action]
        [Parameter("val", typeof(int))]
        public virtual int TestIEEE754Compatibility2(TestIEEE754Compatibility2Parameters parameters)
        {
            int val = parameters.val;
            return val;
        }

        public class TestIEEE754Compatibility3Parameters
        {
            public long val { get; set; }
        }

        [Action]
        [Parameter("val", typeof(long))]
        public virtual long TestIEEE754Compatibility3(TestIEEE754Compatibility3Parameters parameters)
        {
            long val = parameters.val;
            return val;
        }

        public class FirstSecondValueDecimalParameters
        {
            public decimal firstValue { get; set; }

            public decimal secondValue { get; set; }
        }

        [Action]
        [Parameter("firstValue", typeof(decimal))]
        [Parameter("secondValue", typeof(decimal))]
        public virtual decimal TestDecimalSum(FirstSecondValueDecimalParameters parameters)
        {
            return parameters.firstValue + parameters.secondValue;
        }
    }
}
