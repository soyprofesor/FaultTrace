using System.Collections.Generic;
using FaultTrace.Core.Model;

namespace FaultTrace.Core.Localization;

internal static class FaultMessageCatalog
{
    public static Dictionary<string, Dictionary<string, FaultMessage>> Create() =>
        new Dictionary<string, Dictionary<string, FaultMessage>>
        {
            [CultureNormalizer.English] = new Dictionary<string, FaultMessage>
            {
                [FaultCodes.Business] = new FaultMessage("Request could not be completed", "The request could not be completed."),
                [FaultCodes.Validation] = new FaultMessage("Invalid data", "Some of the submitted data is invalid."),
                [FaultCodes.RequiredField] = new FaultMessage("Incomplete data", "A required field is missing.", "A required field is missing: {0}"),
                [FaultCodes.ValueTooLong] = new FaultMessage("Invalid data", "A value is too long.", "A value is too long: {0}"),
                [FaultCodes.InvalidFormat] = new FaultMessage("Invalid data", "A value has an invalid format."),
                [FaultCodes.ReferenceNotFound] = new FaultMessage("Invalid data", "A referenced record was not found."),
                [FaultCodes.Unauthorized] = new FaultMessage("Authentication required", "Please sign in to continue."),
                [FaultCodes.Forbidden] = new FaultMessage("Access denied", "You don't have permission to perform this action."),
                [FaultCodes.NotFound] = new FaultMessage("Not found", "The requested resource was not found."),
                [FaultCodes.Duplicate] = new FaultMessage("Already exists", "This record already exists."),
                [FaultCodes.HasDependents] = new FaultMessage("Record is in use", "This record can't be removed because other records depend on it."),
                [FaultCodes.Unexpected] = new FaultMessage("Unexpected error", "An unexpected error occurred. Please contact support and provide the error ID."),
                [FaultCodes.Unavailable] = new FaultMessage("Service unavailable", "The service is temporarily unavailable. Please try again shortly."),
                [FaultCodes.Timeout] = new FaultMessage("Request timed out", "The operation took too long. Please try again."),
                [FaultCodes.ClientClosed] = new FaultMessage("Request cancelled", "The request was cancelled."),
            },
            [CultureNormalizer.UzbekLatin] = new Dictionary<string, FaultMessage>
            {
                [FaultCodes.Business] = new FaultMessage("So'rov bajarilmadi", "So'rovni bajarib bo'lmadi."),
                [FaultCodes.Validation] = new FaultMessage("Ma'lumotlar noto'g'ri", "Yuborilgan ma'lumotlarning bir qismi noto'g'ri."),
                [FaultCodes.RequiredField] = new FaultMessage("Ma'lumotlar to'liq emas", "Majburiy maydon to'ldirilmagan.", "Majburiy maydon to'ldirilmagan: {0}"),
                [FaultCodes.ValueTooLong] = new FaultMessage("Ma'lumotlar noto'g'ri", "Qiymat juda uzun.", "Qiymat juda uzun: {0}"),
                [FaultCodes.InvalidFormat] = new FaultMessage("Ma'lumotlar noto'g'ri", "Qiymat formati noto'g'ri."),
                [FaultCodes.ReferenceNotFound] = new FaultMessage("Ma'lumotlar noto'g'ri", "Bog'langan yozuv topilmadi."),
                [FaultCodes.Unauthorized] = new FaultMessage("Avtorizatsiya talab qilinadi", "Davom etish uchun tizimga kiring."),
                [FaultCodes.Forbidden] = new FaultMessage("Ruxsat yo'q", "Bu amalni bajarishga ruxsatingiz yo'q."),
                [FaultCodes.NotFound] = new FaultMessage("Topilmadi", "So'ralgan ma'lumot topilmadi."),
                [FaultCodes.Duplicate] = new FaultMessage("Yozuv allaqachon mavjud", "Bunday yozuv allaqachon mavjud."),
                [FaultCodes.HasDependents] = new FaultMessage("Yozuv ishlatilmoqda", "Bu yozuvni o'chirib bo'lmaydi, chunki unga boshqa yozuvlar bog'langan."),
                [FaultCodes.Unexpected] = new FaultMessage("Kutilmagan xatolik", "Kutilmagan xatolik yuz berdi. Xato kodi bilan qo'llab-quvvatlash xizmatiga murojaat qiling."),
                [FaultCodes.Unavailable] = new FaultMessage("Xizmat vaqtincha ishlamayapti", "Xizmat vaqtincha ishlamayapti. Birozdan keyin qayta urinib ko'ring."),
                [FaultCodes.Timeout] = new FaultMessage("Vaqt tugadi", "Amal juda uzoq davom etdi. Qayta urinib ko'ring."),
                [FaultCodes.ClientClosed] = new FaultMessage("So'rov bekor qilindi", "So'rov bekor qilindi."),
            },
            [CultureNormalizer.UzbekCyrillic] = new Dictionary<string, FaultMessage>
            {
                [FaultCodes.Business] = new FaultMessage("Сўров бажарилмади", "Сўровни бажариб бўлмади."),
                [FaultCodes.Validation] = new FaultMessage("Маълумотлар нотўғри", "Юборилган маълумотларнинг бир қисми нотўғри."),
                [FaultCodes.RequiredField] = new FaultMessage("Маълумотлар тўлиқ эмас", "Мажбурий майдон тўлдирилмаган.", "Мажбурий майдон тўлдирилмаган: {0}"),
                [FaultCodes.ValueTooLong] = new FaultMessage("Маълумотлар нотўғри", "Қиймат жуда узун.", "Қиймат жуда узун: {0}"),
                [FaultCodes.InvalidFormat] = new FaultMessage("Маълумотлар нотўғри", "Қиймат формати нотўғри."),
                [FaultCodes.ReferenceNotFound] = new FaultMessage("Маълумотлар нотўғри", "Боғланган ёзув топилмади."),
                [FaultCodes.Unauthorized] = new FaultMessage("Авторизация талаб қилинади", "Давом этиш учун тизимга киринг."),
                [FaultCodes.Forbidden] = new FaultMessage("Рухсат йўқ", "Бу амални бажаришга рухсатингиз йўқ."),
                [FaultCodes.NotFound] = new FaultMessage("Топилмади", "Сўралган маълумот топилмади."),
                [FaultCodes.Duplicate] = new FaultMessage("Ёзув аллақачон мавжуд", "Бундай ёзув аллақачон мавжуд."),
                [FaultCodes.HasDependents] = new FaultMessage("Ёзув ишлатилмоқда", "Бу ёзувни ўчириб бўлмайди, чунки унга бошқа ёзувлар боғланган."),
                [FaultCodes.Unexpected] = new FaultMessage("Кутилмаган хатолик", "Кутилмаган хатолик юз берди. Хато коди билан қўллаб-қувватлаш хизматига мурожаат қилинг."),
                [FaultCodes.Unavailable] = new FaultMessage("Хизмат вақтинча ишламаяпти", "Хизмат вақтинча ишламаяпти. Бироздан кейин қайта уриниб кўринг."),
                [FaultCodes.Timeout] = new FaultMessage("Вақт тугади", "Амал жуда узоқ давом этди. Қайта уриниб кўринг."),
                [FaultCodes.ClientClosed] = new FaultMessage("Сўров бекор қилинди", "Сўров бекор қилинди."),
            },
            [CultureNormalizer.Russian] = new Dictionary<string, FaultMessage>
            {
                [FaultCodes.Business] = new FaultMessage("Запрос не выполнен", "Не удалось выполнить запрос."),
                [FaultCodes.Validation] = new FaultMessage("Некорректные данные", "Часть отправленных данных некорректна."),
                [FaultCodes.RequiredField] = new FaultMessage("Данные неполные", "Не заполнено обязательное поле.", "Не заполнено обязательное поле: {0}"),
                [FaultCodes.ValueTooLong] = new FaultMessage("Некорректные данные", "Значение слишком длинное.", "Значение слишком длинное: {0}"),
                [FaultCodes.InvalidFormat] = new FaultMessage("Некорректные данные", "Неверный формат значения."),
                [FaultCodes.ReferenceNotFound] = new FaultMessage("Некорректные данные", "Связанная запись не найдена."),
                [FaultCodes.Unauthorized] = new FaultMessage("Требуется авторизация", "Войдите в систему, чтобы продолжить."),
                [FaultCodes.Forbidden] = new FaultMessage("Доступ запрещён", "У вас нет прав для выполнения этого действия."),
                [FaultCodes.NotFound] = new FaultMessage("Не найдено", "Запрошенные данные не найдены."),
                [FaultCodes.Duplicate] = new FaultMessage("Запись уже существует", "Такая запись уже существует."),
                [FaultCodes.HasDependents] = new FaultMessage("Запись используется", "Эту запись нельзя удалить, так как с ней связаны другие записи."),
                [FaultCodes.Unexpected] = new FaultMessage("Непредвиденная ошибка", "Произошла непредвиденная ошибка. Обратитесь в службу поддержки и укажите код ошибки."),
                [FaultCodes.Unavailable] = new FaultMessage("Сервис временно недоступен", "Сервис временно недоступен. Повторите попытку немного позже."),
                [FaultCodes.Timeout] = new FaultMessage("Время ожидания истекло", "Операция заняла слишком много времени. Повторите попытку."),
                [FaultCodes.ClientClosed] = new FaultMessage("Запрос отменён", "Запрос был отменён."),
            },
        };
}
