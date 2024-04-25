﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EduNexBL.DTOs;

namespace EduNexBL.IRepository
{
    public interface IAdminRepository
    {
        Task<IEnumerable<TeacherDto>> GetTeachersAsync();
        Task<bool> ApproveTeacherAsync(string id);
        Task<bool> RejectTeacherAsync(string id);
        Task<TeacherDto> GetTeacherByIdAsync(string id);
        Task<StudentDto> GetStudentByIdAsync(string id);
        Task<IEnumerable<UserDto>> SearchUsersAsync(SearchQuery query);
        Task<IEnumerable<StudentDto>> GetStudentsAsync();
    }
}