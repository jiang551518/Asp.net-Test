﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using MiniExcelLibs;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Asp.net_Test1
{
    [Route("api/[controller]")]
    public class TestController : BaseController
    {
        protected ITestService _testService { get; set; }
        public TestController(ITestService testService, Microsoft.Extensions.Configuration.IConfiguration configuration) : base(configuration)
        {
            _testService = testService;
        }

        /// <summary>
        /// 测试列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetList")]
        public async Task<TestVM> GetList()
        {
            var result = await _testService.GetList();
            return result;
        }

        /// <summary>
        /// 测试列表导出（导出加密）
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetListExport")]
        public async Task<IActionResult> GetListExport()
        {
            // 设置 EPPlus 许可上下文
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;  

            var exportData = await _testService.GetListExport();

            var memoryStream = new MemoryStream();
            memoryStream.SaveAs(exportData);

            using (var package = new ExcelPackage(memoryStream))
            {
                var encryption = package.Encryption;
                encryption.Password = user.ExcelPasswd;  

                var encryptedMemoryStream = new MemoryStream();
                package.SaveAs(encryptedMemoryStream);

                encryptedMemoryStream.Seek(0, SeekOrigin.Begin);

                HttpContext.Response.Headers.Clear();
                HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
                HttpContext.Response.Headers.Add("Content-Disposition", $"attachment;filename={DateTime.Now.ToString("yyyyMMddhhmmss")}.xlsx");
                return File(encryptedMemoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }

        /// <summary>
        /// 查看天气
        /// </summary>
        /// <param name="city">城市</param>
        /// <returns></returns>
        [HttpGet("GetWeather")]
        public async Task<WeatherVM> GetWeather(string city)
        {
            var result = await _testService.GetWeather(city);
            if (result.showapi_res_code == 0)
            {
                var response = new WeatherVM()
                {
                    status = "0",
                    msg = "ok"
                };
                response.result = new WeatherResultVM()
                {
                    city = result.showapi_res_body.cityInfo.c3,
                    date = DateTime.Now.Date.ToString(),
                    week = DateTime.Now.DayOfWeek.ToString(),
                    weather = result.showapi_res_body.now.weather,
                    temp = result.showapi_res_body.now.temperature,
                    temphigh = result.showapi_res_body.f1.day_air_temperature,
                    templow = result.showapi_res_body.f1.night_air_temperature,
                    img = result.showapi_res_body.now.weather_pic,
                    updatetime = result.showapi_res_body.now.temperature_time,
                };
                return response;
            }
            else
                return null;
        }

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="username"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        [HttpGet("GetUserList")]
        public async Task<List<User>> GetUserList(string username, bool? enabled)
        {
            var result = await _testService.GetUserList(username,enabled);
            return result;
        }

        /// <summary>
        /// 编辑用户
        /// </summary>
        /// <param name="id"></param>
        /// <param name="username"></param>
        /// <param name="pwd"></param>
        /// <param name="isEnable"></param>
        /// <param name="roleType"></param>
        /// <param name="excelPasswd"></param>
        /// <returns></returns>
        [HttpPut("EditUser")]
        public async Task<bool> EditUser(Guid id, string username, string pwd, bool isEnable, RoleType roleType,string excelPasswd)
        {
            var redisClient = new FreeRedis.RedisClient("127.0.0.1:6379");
            bool isContinue = false;
            lock (this)
            {
                var cacheValue = redisClient.Get("test_" + username);
                if (string.IsNullOrWhiteSpace(cacheValue))
                {
                    // 如果 Redis 中没有记录，或者记录为空，则可以继续操作
                    isContinue = true;
                }
                else
                {
                    var lastExecutionTime = DateTime.Parse(cacheValue);
                    var currentTime = DateTime.Now;

                    // 计算距离上次执行时间的间隔
                    var timeSinceLastExecution = currentTime - lastExecutionTime;
                    // 如果距离上次执行时间超过 7 秒，则可以继续操作 （增加容错）
                    if (timeSinceLastExecution.TotalSeconds >= 7)
                    {
                        isContinue = true;
                        redisClient.Set("test_" + username.ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                }
            }
            var isSuccess = await _testService.EditUser(id, username, pwd, isEnable, roleType, user, excelPasswd);
            return isSuccess;
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("UpdateIsDelete")]
        public async Task<bool> UpdateIsDelete(Guid id)
        {
            var isSuccess = await _testService.UpdateIsDelete(id, user);
            return isSuccess;
        }

        /// <summary>
        /// 获取用户详情(接口204说明没有查到数据)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("GetDetail")]
        public async Task<User> GetDetail(Guid id)
        {
            var userDetail = await _testService.GetDetail(id);
            return userDetail;
        }
    }
}
