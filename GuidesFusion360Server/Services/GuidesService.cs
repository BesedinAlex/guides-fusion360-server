using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IMapper _mapper;
        private readonly DataContext _context;
        private readonly IAuthRepository _authRepo;
        private readonly IFileManager _fileManager;

        public GuidesService(IMapper mapper, DataContext context, IAuthRepository authRepo, IFileManager fileManager)
        {
            _mapper = mapper;
            _context = context;
            _authRepo = authRepo;
            _fileManager = fileManager;
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

            serviceResponse.Data = await _fileManager.GetFile(guideId, "preview.png", "image/png");
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

            serviceResponse.Data = await _fileManager.GetFile(guideId, "preview.png", "image/png");
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

        public async Task<ServiceResponse<int>> CreateNewGuide(int ownerId, AddNewGuideDto newGuide)
        {
            var serviceResponse = new ServiceResponse<int>();

            var guide = _mapper.Map<Guide>(newGuide);
            guide.OwnerId = ownerId;
            guide.Hidden = "true";

            var image = newGuide.Image;
            if (image.ContentType != "image/png")
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Image must be png.";
                return serviceResponse;
            }

            await _context.Guides.AddAsync(guide);
            await _context.SaveChangesAsync();

            await _fileManager.SaveFile(guide.Id, "preview.png", image);

            serviceResponse.Data = guide.Id;
            serviceResponse.Message = "Guide is added.";
            return serviceResponse;
        }

        private async Task<Tuple<bool, ServiceResponse<T>, int>> GuideIsAvailable<T>(int guideId)
        {
            var serviceResponse = new ServiceResponse<T>();
            
            var guide = await _context.Guides.FirstOrDefaultAsync(x => x.Id == guideId);
            
            if (guide == null)
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide with that id was not found.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 404);
            }

            if (guide.Hidden == "true")
            {
                serviceResponse.Success = false;
                serviceResponse.Message = "Guide with that id is not public. You should provide token to access it.";
                return new Tuple<bool, ServiceResponse<T>, int>(false, serviceResponse, 401);
            }

            return new Tuple<bool, ServiceResponse<T>, int>(true, serviceResponse, 200);
        }
    }
}
