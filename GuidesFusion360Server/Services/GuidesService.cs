using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using GuidesFusion360Server.Data;
using GuidesFusion360Server.Dtos;
using GuidesFusion360Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuidesFusion360Server.Services
{
    public class GuidesService : IGuidesService
    {
        private class ConverterResponse
        {
            public string content { get; set; }
            
            public string thumbnail { get; set; }
        }
        
        private readonly IMapper _mapper;
        private readonly DataContext _context;
        private readonly IAuthRepository _authRepo;
        private readonly IFileManager _fileManager;
        private readonly IHttpClientFactory _clientFactory;

        public GuidesService(IMapper mapper, DataContext context, IAuthRepository authRepo, IFileManager fileManager,
            IHttpClientFactory clientFactory)
        {
            _mapper = mapper;
            _context = context;
            _authRepo = authRepo;
            _fileManager = fileManager;
            _clientFactory = clientFactory;
        }

        public async Task<ServiceResponse<List<GetAllGuidesDto>>> GetAllGuides()
        {
            var serviceResponse = new ServiceResponse<List<GetAllGuidesDto>>();
            var guides = await _context.Guides.Where(x => x.Hidden == "false").ToListAsync();
            serviceResponse.Data = guides.Select(c => _mapper.Map<GetAllGuidesDto>(c)).ToList();
            return serviceResponse;
        }

        public async Task<ServiceResponse<List<GetAllGuidesDto>>> GetAllHiddenGuides(int userId)
        {
            var serviceResponse = new ServiceResponse<List<GetAllGuidesDto>>();
            var hasAccess = await _authRepo.UserIsEditor(userId);
            if (!hasAccess)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "User access should be editor or admin.";
                return serviceResponse;
            }

            var guides = await _context.Guides.Where(x => x.Hidden == "true").ToListAsync();
            serviceResponse.Data = guides.Select(c => _mapper.Map<GetAllGuidesDto>(c)).ToList();
            return serviceResponse;
        }

        public async Task<Tuple<ServiceResponse<FileContentResult>, int>> GetPublicGuidePreview(int guideId)
        {
            var serviceResponse = new ServiceResponse<FileContentResult>();

            var (isAvailable, accessResponse, statusCode) = await GuideIsAvailable<FileContentResult>(guideId);
            if (!isAvailable)
            {
                return new Tuple<ServiceResponse<FileContentResult>, int>(accessResponse, statusCode);
            }

            var file = await _fileManager.GetFile(guideId, "preview.png");
            serviceResponse.Data = new FileContentResult(file, "image/png");
            return new Tuple<ServiceResponse<FileContentResult>, int>(serviceResponse, 200);
        }

        public async Task<ServiceResponse<FileContentResult>> GetPrivateGuidePreview(int guideId, int userId)
        {
            var serviceResponse = new ServiceResponse<FileContentResult>();

            var hasAccess = await _authRepo.UserIsEditor(userId);

            if (!hasAccess)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "User access should be editor or admin.";
                return serviceResponse;
            }

            var file = await _fileManager.GetFile(guideId, "preview.png");
            serviceResponse.Data = new FileContentResult(file, "image/png");
            return serviceResponse;
        }

        public async Task<Tuple<ServiceResponse<List<GetAllPartGuidesDto>>, int>> GetAllPublicPartGuides(int guideId)
        {
            var serviceResponse = new ServiceResponse<List<GetAllPartGuidesDto>>();

            var (isAvailable, accessResponse, statusCode) = await GuideIsAvailable<List<GetAllPartGuidesDto>>(guideId);
            if (!isAvailable)
            {
                return new Tuple<ServiceResponse<List<GetAllPartGuidesDto>>, int>(accessResponse, statusCode);
            }

            var guides = await _context.PartGuides.Where(x => x.GuideId == guideId).ToListAsync();
            serviceResponse.Data = guides.Select(c => _mapper.Map<GetAllPartGuidesDto>(c)).ToList();
            return new Tuple<ServiceResponse<List<GetAllPartGuidesDto>>, int>(serviceResponse, 200);
        }

        public async Task<ServiceResponse<List<GetAllPartGuidesDto>>> GetAllPrivatePartGuides(int guideId, int userId)
        {
            var serviceResponse = new ServiceResponse<List<GetAllPartGuidesDto>>();

            var hasAccess = await _authRepo.UserIsEditor(userId);

            if (!hasAccess)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "User access should be editor or admin.";
                return serviceResponse;
            }

            var guides = await _context.PartGuides.Where(x => x.GuideId == guideId).ToListAsync();
            serviceResponse.Data = guides.Select(c => _mapper.Map<GetAllPartGuidesDto>(c)).ToList();
            return serviceResponse;
        }

        public async Task<Tuple<ServiceResponse<int>, int>> CreateNewGuide(int ownerId, AddNewGuideDto newGuide)
        {
            var serviceResponse = new ServiceResponse<int>();

            var hasAccess = await _authRepo.UserIsEditor(ownerId);
            if (!hasAccess)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "User access should be editor or admin.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 401);
            }

            if (newGuide.Image.ContentType != "image/png")
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "File must be PNG image.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
            }

            var guide = _mapper.Map<Guide>(newGuide);
            guide.OwnerId = ownerId;
            guide.Hidden = "true";

            await _context.Guides.AddAsync(guide);
            await _context.SaveChangesAsync();

            await _fileManager.SaveFile(guide.Id, "preview.png", newGuide.Image);

            serviceResponse.Data = guide.Id;
            serviceResponse.Message = "Guide is successfully added.";
            return new Tuple<ServiceResponse<int>, int>(serviceResponse, 200);
        }

        public async Task<Tuple<ServiceResponse<int>, int>> CreateNewPartGuide(int ownerId, AddNewPartGuideDto newPartGuide)
        {
            var serviceResponse = new ServiceResponse<int>();

            var (isEditable, accessResponse, statusCode) = await GuideIsEditable<int>(ownerId, newPartGuide.GuideId);
            if (!isEditable)
            {
                return new Tuple<ServiceResponse<int>, int>(accessResponse, statusCode);
            }

            var content = newPartGuide.Content;
            var file = newPartGuide.File;

            if (file == null && content == null || file != null && content != null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message =
                    "You should provide PDF or ZIP in file field or YouTube link in content field.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
            }

            var partGuide = _mapper.Map<PartGuide>(newPartGuide);

            if (content != null && !Uri.IsWellFormedUriString(content, UriKind.Absolute))
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "You should provide valid URL in link field.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
            }
            
            if (file != null)
            {
                var isPdf = file.ContentType == "application/pdf";
                var isZip = file.ContentType == "application/zip" || file.ContentType == "application/x-zip-compressed";
                if (!isPdf && !isZip)
                {
                    serviceResponse.Success = false;
                    serviceResponse.Message = "You should provide PDF or ZIP file.";
                    return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
                }

                if (_fileManager.FileExists(partGuide.GuideId, file.FileName))
                {
                    serviceResponse.Success = false;
                    serviceResponse.Message = "File with this name already exists in this guide.";
                    return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
                }

                partGuide.Content = file.FileName;
                await _fileManager.SaveFile(partGuide.GuideId, file.FileName, file);
            }

            await _context.PartGuides.AddAsync(partGuide);
            await _context.SaveChangesAsync();
            serviceResponse.Data = partGuide.Id;
            serviceResponse.Message = "Part guide is successfully added.";
            return new Tuple<ServiceResponse<int>, int>(serviceResponse, 200);
        }

        public async Task<Tuple<ServiceResponse<object>, int>> UploadModel(int ownerId, AddNewGuideModelDto newModel)
        {
            throw new NotImplementedException();
            
            var serviceResponse = new ServiceResponse<object>();

            var (isEditable, accessResponse, statusCode) = await GuideIsEditable<object>(ownerId, newModel.GuideId);
            if (!isEditable)
            {
                return new Tuple<ServiceResponse<object>, int>(accessResponse, statusCode);
            }

            var file = newModel.File;

            await _fileManager.SaveFile(newModel.GuideId, "model.stp", file);
            var stpFs = _fileManager.GetFileStream(newModel.GuideId, "model.stp");

            using var formData = new MultipartFormDataContent("12345");
            // formData.Headers.ContentType.MediaType = "multipart/form-data";
            // formData.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            formData.Add(new StreamContent(stpFs), "file", "model.stp");
            formData.Add(new StringContent("stp"), "from");
            formData.Add(new StringContent("glb"), "to");
            // formData.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=12345");
            
            
            using var client = _clientFactory.CreateClient("converter");
            var response = await client.PostAsync("/model", formData);
            
            serviceResponse.Data = await response.Content.ReadAsStringAsync();  
            return new Tuple<ServiceResponse<object>, int>(serviceResponse, 200);
            
            // await _fileManager.SaveFile(newModel.GuideId, "model.glb", file);
        }

        public async Task<Tuple<ServiceResponse<int>, int>> ChangeGuideVisibility(int ownerId, int guideId, string hidden)
        {
            var serviceResponse = new ServiceResponse<int>();

            var (isEditable, accessResponse, statusCode) = await GuideIsEditable<int>(ownerId, guideId, true);
            if (!isEditable)
            {
                return new Tuple<ServiceResponse<int>, int>(accessResponse, statusCode);
            }

            if (hidden != "true" && hidden != "false")
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Hidden field should be true or false.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
            }

            var guide = await _context.Guides.FirstOrDefaultAsync(x => x.Id == guideId);
            guide.Hidden = hidden;
            _context.Guides.Update(guide);
            await _context.SaveChangesAsync();

            serviceResponse.Data = guideId;
            serviceResponse.Message = hidden == "true" ? "Guide is now hidden." : "Guide is now public.";
            return new Tuple<ServiceResponse<int>, int>(serviceResponse, 200);
        }

        public async Task<Tuple<ServiceResponse<int>, int>> UpdatePartGuide(int ownerId, int partGuideId,
            UpdatePartGuideDto updatedGuide)
        {
            var serviceResponse = new ServiceResponse<int>();

            var (isEditable, accessResponse, statusCode) = await PartGuideIsEditable<int>(ownerId, partGuideId);
            if (!isEditable)
            {
                return new Tuple<ServiceResponse<int>, int>(accessResponse, statusCode);
            }

            var name = updatedGuide.Name;
            var content = updatedGuide.Content;
            var file = updatedGuide.File;
            
            if (file != null && content != null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "You cannot provide file and content at the same time.";
                return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
            }

            var partGuide = await _context.PartGuides.FirstOrDefaultAsync(x => x.Id == partGuideId);

            if (content != null)
            {
                if (!Uri.IsWellFormedUriString(content, UriKind.Absolute))
                {
                    serviceResponse.Success = false;
                    serviceResponse.Message = "You should provide valid URL in content field.";
                    return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
                }

                if (_fileManager.FileExists(partGuide.GuideId, partGuide.Content))
                {
                    _fileManager.DeleteFile(partGuide.GuideId, partGuide.Content);
                }
                partGuide.Content = content;
            }
            
            if (file != null)
            {
                var isPdf = file.ContentType == "application/pdf";
                var isZip = file.ContentType == "application/zip" || file.ContentType == "application/x-zip-compressed";
                if (!isPdf && !isZip)
                {
                    serviceResponse.Success = false;
                    serviceResponse.Message = "You should provide PDF or ZIP file or no files at all.";
                    return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
                }
                
                if (_fileManager.FileExists(partGuide.GuideId, file.FileName))
                {
                    if (file.FileName != partGuide.Content)
                    {
                        serviceResponse.Success = false;
                        serviceResponse.Message = "File with this name already exists in this guide.";
                        return new Tuple<ServiceResponse<int>, int>(serviceResponse, 400);
                    }
                    await _fileManager.SaveFile(partGuide.GuideId, file.FileName, file);
                }
                
                if (_fileManager.FileExists(partGuide.GuideId, partGuide.Content))
                {
                    _fileManager.DeleteFile(partGuide.GuideId, partGuide.Content);
                }
                partGuide.Content = file.FileName;
                await _fileManager.SaveFile(partGuide.GuideId, file.FileName, file);
            }

            if (name != null)
            {
                partGuide.Name = name;
            }

            _context.PartGuides.Update(partGuide);
            await _context.SaveChangesAsync();
            
            serviceResponse.Data = partGuide.Id;
            serviceResponse.Message = "Part guide is successfully updated.";
            return new Tuple<ServiceResponse<int>, int>(serviceResponse, 200);
        }

        private async Task<Tuple<bool, ServiceResponse<T>, int>> GuideIsAvailable<T>(int guideId)
        {
            var serviceResponse = new ServiceResponse<T>();

            var guide = await _context.Guides.FirstOrDefaultAsync(x => x.Id == guideId);

            if (guide == null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide with this id was not found.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 404);
            }

            if (guide.Hidden == "true")
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide with this id is not public. You should provide token to access it.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 401);
            }

            return new Tuple<bool, ServiceResponse<T>, int>(true, serviceResponse, 200);
        }

        private async Task<Tuple<bool, ServiceResponse<T>, int>> GuideIsEditable<T>(int userId, int guideId,
            bool admin = false)
        {
            var serviceResponse = new ServiceResponse<T>();

            var hasAccess = admin ? await _authRepo.UserIsAdmin(userId) : await _authRepo.UserIsEditor(userId);

            if (!hasAccess)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = $"User access should be {(admin ? "admin." : "editor or admin.")}";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 401);
            }

            var guide = await _context.Guides.FirstOrDefaultAsync(x => x.Id == guideId);

            if (guide == null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide with this id was not found.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 404);
            }

            if (guide.Hidden == "false" && !admin)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide should be hidden in order to edit it.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 400);
            }

            return new Tuple<bool, ServiceResponse<T>, int>(true, serviceResponse, 200);
        }

        private async Task<Tuple<bool, ServiceResponse<T>, int>> PartGuideIsEditable<T>(int userId, int partGuideId)
        {
            var serviceResponse = new ServiceResponse<T>();

            var partGuide = await _context.PartGuides.FirstOrDefaultAsync(x => x.Id == partGuideId);
            
            if (partGuide == null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Part guide with this id was not found.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 404);
            }

            return await GuideIsEditable<T>(userId, partGuide.GuideId);
        }
    }
}
