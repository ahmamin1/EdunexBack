﻿using Amazon.Util.Internal;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduNexBL.DTOs.CourseDTOs
{
    public class AttachmentAddDto
    {
        [Required]
        public string AttachmentTitle { get; set; }
        [Required]
        public IFormFile File { get; set; }
        [Required]
        public int LectureId { get; set; }

    }

}