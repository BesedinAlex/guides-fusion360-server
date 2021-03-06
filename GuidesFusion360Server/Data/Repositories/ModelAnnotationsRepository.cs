using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuidesFusion360Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GuidesFusion360Server.Data.Repositories
{
    public class ModelAnnotationsRepository : IModelAnnotationsRepository
    {
        private readonly DataContext _context;

        public ModelAnnotationsRepository(DataContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task<ModelAnnotation> GetAnnotation(int annotationId) =>
            _context.ModelAnnotations.FirstOrDefaultAsync(x => x.Id == annotationId);

        /// <inheritdoc />
        public Task<List<ModelAnnotation>> GetAnnotations(int guideId) =>
            _context.ModelAnnotations.Where(x => x.GuideId == guideId).ToListAsync();

        /// <inheritdoc />
        public async Task<int> AddAnnotation(ModelAnnotation annotation)
        {
            await _context.ModelAnnotations.AddAsync(annotation);
            await _context.SaveChangesAsync();

            return annotation.Id;
        }

        /// <inheritdoc />
        public Task DeleteAnnotation(ModelAnnotation annotation)
        {
            _context.ModelAnnotations.Remove(annotation);
            return _context.SaveChangesAsync();
        }

        /// <inheritdoc />
        public Task DeleteAnnotations(List<ModelAnnotation> annotations)
        {
            _context.ModelAnnotations.RemoveRange(annotations);
            return _context.SaveChangesAsync();
        }
    }
}