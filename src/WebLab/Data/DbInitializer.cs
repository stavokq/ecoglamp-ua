using Microsoft.EntityFrameworkCore;
using WebLab.Models;

namespace WebLab.Data;

public static class DbInitializer
{
    public static async Task SeedDataAsync(ApplicationDbContext context)
    {
        if (await context.Regions.AnyAsync())
        {
            return;
        }

        var rCarpathians = new Region
        {
            Name = "Карпати",
            Description = "Мальовничі гірські хребти, смерекові ліси, водоспади та чисте гірське повітря."
        };
        var rShatsk = new Region
        {
            Name = "Шацькі озера",
            Description = "Унікальний озерний край Волині, кришталево чистий Світязь та піщані пляжі."
        };
        var rDniester = new Region
        {
            Name = "Дністровський каньйон",
            Description = "Одне із семи природних чудес України з меандрами річки, скелями та теплою мікрокліматичною зоною."
        };
        var rTovtry = new Region
        {
            Name = "Подільські Товтри",
            Description = "Залишки прадавнього коралового бар'єрного рифу та мальовнича Бакота."
        };
        var rPolissia = new Region
        {
            Name = "Київське Полісся",
            Description = "Соснові ліси, річки Тетерів та Десна, спокійний відпочинок поблизу столиці."
        };

        await context.Regions.AddRangeAsync(rCarpathians, rShatsk, rDniester, rTovtry, rPolissia);
        await context.SaveChangesAsync();

        var actChan = new Activity { Name = "Карпатський чан", IconClass = "bi-water" };
        var actSauna = new Activity { Name = "Панорамна сауна", IconClass = "bi-fire" };
        var actHiking = new Activity { Name = "Хайкінг та трекінг", IconClass = "bi-compass" };
        var actKayaks = new Activity { Name = "Прокат каяків та SUP", IconClass = "bi-tsunami" };
        var actPetFriendly = new Activity { Name = "Pet-Friendly", IconClass = "bi-heart" };
        var actStarlink = new Activity { Name = "Starlink & Wi-Fi", IconClass = "bi-wifi" };
        var actBbq = new Activity { Name = "Барбекю-зона", IconClass = "bi-cup-hot" };
        var actAstronomy = new Activity { Name = "Телескоп / Зорі", IconClass = "bi-moon-stars" };

        await context.Activities.AddRangeAsync(actChan, actSauna, actHiking, actKayaks, actPetFriendly, actStarlink, actBbq, actAstronomy);
        await context.SaveChangesAsync();

        var g1 = new GlampingSite
        {
            Name = "Edem Mountain Domes",
            Description = "Геодезичні куполи на схилі гори з панорамним видом на Чорногірський хребет. Власний чан на дровах під зоряним небом.",
            Address = "с. Татарів, урочище Піги, Івано-Франківська обл.",
            Latitude = 48.3421,
            Longitude = 24.5782,
            PricePerNight = 4200m,
            MaxGuests = 4,
            ImageUrl = "https://images.unsplash.com/photo-1510312305653-8ed496efae75?auto=format&fit=crop&w=1200&q=80",
            DocumentUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
            ContactPhone = "+380671112233",
            RegionId = rCarpathians.Id
        };

        var g2 = new GlampingSite
        {
            Name = "Shatsk Lake Silence",
            Description = "Еко-намети преміум-класу на першій лінії озера Світязь. Приватний пірс, каяки та тиша хвойного лісу.",
            Address = "урочище Гряда, Шацький р-н, Волинська обл.",
            Latitude = 51.5034,
            Longitude = 23.8512,
            PricePerNight = 3100m,
            MaxGuests = 3,
            ImageUrl = "https://images.unsplash.com/photo-1520250497591-112f2f40a3f4?auto=format&fit=crop&w=1200&q=80",
            DocumentUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
            ContactPhone = "+380504445566",
            RegionId = rShatsk.Id
        };

        var g3 = new GlampingSite
        {
            Name = "Bakota Sunset Glamp",
            Description = "Глемпінг над затопленою Бакотою з неймовірними заходами сонця. Каньйон, тепле море Поділля та скельний монастир поруч.",
            Address = "с. Колодіївка, Кам'янець-Подільський р-н, Хмельницька обл.",
            Latitude = 48.5833,
            Longitude = 26.9944,
            PricePerNight = 3600m,
            MaxGuests = 2,
            ImageUrl = "https://images.unsplash.com/photo-1470246973918-29a93221c455?auto=format&fit=crop&w=1200&q=80",
            DocumentUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
            ContactPhone = "+380637778899",
            RegionId = rTovtry.Id
        };

        var g4 = new GlampingSite
        {
            Name = "Polissia Pine Safari",
            Description = "Сафарі-тенти серед високих сосен поблизу Київського моря. Ідеальне місце для перезавантаження на вихідних.",
            Address = "с. Глібівка, Вишгородський р-н, Київська обл.",
            Latitude = 50.7712,
            Longitude = 30.3456,
            PricePerNight = 2800m,
            MaxGuests = 4,
            ImageUrl = "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=1200&q=80",
            DocumentUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
            ContactPhone = "+380993332211",
            RegionId = rPolissia.Id
        };

        await context.GlampingSites.AddRangeAsync(g1, g2, g3, g4);
        await context.SaveChangesAsync();

        var links = new List<GlampingActivity>
        {
            new() { GlampingSiteId = g1.Id, ActivityId = actChan.Id },
            new() { GlampingSiteId = g1.Id, ActivityId = actSauna.Id },
            new() { GlampingSiteId = g1.Id, ActivityId = actHiking.Id },
            new() { GlampingSiteId = g1.Id, ActivityId = actStarlink.Id },
            new() { GlampingSiteId = g1.Id, ActivityId = actAstronomy.Id },

            new() { GlampingSiteId = g2.Id, ActivityId = actKayaks.Id },
            new() { GlampingSiteId = g2.Id, ActivityId = actPetFriendly.Id },
            new() { GlampingSiteId = g2.Id, ActivityId = actBbq.Id },
            new() { GlampingSiteId = g2.Id, ActivityId = actStarlink.Id },

            new() { GlampingSiteId = g3.Id, ActivityId = actKayaks.Id },
            new() { GlampingSiteId = g3.Id, ActivityId = actHiking.Id },
            new() { GlampingSiteId = g3.Id, ActivityId = actAstronomy.Id },

            new() { GlampingSiteId = g4.Id, ActivityId = actPetFriendly.Id },
            new() { GlampingSiteId = g4.Id, ActivityId = actBbq.Id },
            new() { GlampingSiteId = g4.Id, ActivityId = actSauna.Id }
        };

        await context.GlampingActivities.AddRangeAsync(links);

        var reviews = new List<Review>
        {
            new() { GlampingSiteId = g1.Id, AuthorName = "Олександр П.", Rating = 5, Comment = "Неймовірні краєвиди та гарячий чан після походу на Говерлу!" },
            new() { GlampingSiteId = g1.Id, AuthorName = "Марія К.", Rating = 5, Comment = "Дуже затишно, тепло всередині, ліжко дуже зручне." },
            new() { GlampingSiteId = g2.Id, AuthorName = "Дмитро С.", Rating = 4, Comment = "Озеро поруч, чудові каяки. Хотілося б більше столиків на терасі." },
            new() { GlampingSiteId = g3.Id, AuthorName = "Олена В.", Rating = 5, Comment = "Заходи сонця над Бакотою неможливо описати словами! Обов'язково повернемось." }
        };

        await context.Reviews.AddRangeAsync(reviews);
        await context.SaveChangesAsync();
    }
}
