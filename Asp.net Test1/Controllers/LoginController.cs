﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Asp.net_Test1
{
    [Route("api/[controller]")]
    public class LoginController : Controller
    {
        protected ITestService _testService { get; set; }

        protected IMapper _mapper { get; set; }

        protected IConfiguration _configuration { get; }
        public LoginController(ITestService testService, IConfiguration configuration, IMapper mapper)
        {
            _testService = testService;
            _configuration = configuration;
            _mapper = mapper;
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("Login")]
        public async Task<LoginVM> Login([FromBody] LoginInputVM input)
        {
            var result = new LoginVM();
            var userDetail = await _testService.GetUserDetail(input.Username);
            if (userDetail != null)
            {
                if (userDetail.pwd != input.Pwd) { throw new Exception("密码错误!"); };
                if (userDetail.Enabled == false) { throw new Exception("用户已禁用"); };
                result = new LoginVM() 
                {
                    UserName = userDetail.username,
                    Id = userDetail.Id ,
                    Enable = userDetail.Enabled
                };
                result.JwtToken = await CreateJwtAndSaveCache(result);
            }
            else
            {
                throw new Exception("该用户不存在");
            }
            return result;
        }

        #region Jwt Token
        private async Task<string> CreateJwtAndSaveCache(LoginVM vm)
        {
            var invalidTime = DateTime.Now.Add(TimeSpan.FromDays(7));
            var invalidTimeTicks = invalidTime.Ticks;
            //生成JWT Token
            var claims = new Claim[]
            {
                    new Claim(JwtRegisteredClaimNames.Sub,"test"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Sid, vm.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, invalidTimeTicks.ToString(), ClaimValueTypes.Integer64)

            };

            var jwt = new JwtSecurityToken(
                        claims: claims,
                        expires: invalidTime
                    );
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
            var identity = new ClaimsIdentity(claims, "msls");
            //将用户数据放入Identity
            User.AddIdentity(identity);
            var redisClient = new FreeRedis.RedisClient("127.0.0.1:6379"); // 根据你的配置修改连接地址
            await redisClient.SetAsync("user_" + vm.Id.ToString(), JsonExtensions.SerializeToJson(vm));
            return encodedJwt;
        }
        #endregion


        /// <summary>
        /// 注册
        /// </summary>
        /// <returns></returns>
        [HttpPost("Sign")]
        public async Task<bool> Sign([FromBody] SignVM vm)
        {
            var redisClient = new FreeRedis.RedisClient("127.0.0.1:6379");
            bool isContinue = false;
            lock (this)
            {
                var cacheValue = redisClient.Get("test_" + vm.Username);
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
                        redisClient.Set("test_" + vm.Username.ToString(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                }
            }

            var input = _mapper.Map<User>(vm);
            var isSuccess = await _testService.Sign(input);
            return isSuccess;
        }

        /// <summary>
        /// 编辑用户
        /// </summary>
        /// <param name="id"></param>
        /// <param name="username"></param>
        /// <param name="pwd"></param>
        /// <param name="isEnable"></param>
        /// <returns></returns>
        [HttpPut("EditUser")]
        public async Task<bool> EditUser(Guid id, string username, string pwd, bool isEnable)
        {
            var isSuccess = await _testService.EditUser(id, username, pwd, isEnable);
            return isSuccess;
        }
    }
}
