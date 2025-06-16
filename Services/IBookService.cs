using Moyu.Models;
using System.Collections.Generic;

namespace Moyu.Services
{ 
    /// <summary>
    /// �鼮����ӿڣ������˼����鼮���½ڵ�������ҳ�����ݻ�ȡ��ͨ�ò�����
    /// ֧�� TXT��EPUB �ȶ��ָ�ʽ��ʵ�֡�
    /// </summary>
    public interface IBookService
    {
        /// <summary>
        /// �����ļ�·����ȡ�鼮������Ϣ�����������ݣ���
        /// </summary>
        /// <param name="filePath">�鼮�ļ�·��</param>
        /// <returns>�鼮��Ϣ����</returns>
        BookInfo GetBookInfo(string filePath);

        /// <summary>
        /// ����ָ���鼮���ݣ���ʼ���½ڡ���ҳ�����ݡ�
        /// </summary>
        /// <param name="book">Ҫ���ص��鼮��Ϣ</param>
        void LoadBook(BookInfo book);

        /// <summary>
        /// ��ȡָ����Χ�ڵ��½���Ϣ�������½ڷ�ҳ��ʾ����
        /// </summary>
        /// <param name="start">��ʼ�½�������������</param>
        /// <param name="end">�����½���������������</param>
        /// <returns>�½���Ϣ�б�</returns>
        List<ChapterInfo> GetChaptersPage(int start, int end);

        /// <summary>
        /// ��ȡ�½�������
        /// </summary>
        /// <returns>�½�����</returns>
        int GetChaptersCount();

        /// <summary>
        /// ��ת��ָ���½��ڵ�ĳһ�У�ƫ�ƣ���
        /// </summary>
        /// <param name="chapterIndex">�½�����</param>
        /// <param name="lineOffset">�½�����ƫ��</param>
        void JumpToLineInChapter(int chapterIndex, int lineOffset);

        /// <summary>
        /// ��ȡ��ǰҳ�����ݣ����䲻ͬ��ʽ�ķ�ҳ����
        /// </summary>
        /// <returns>��ǰҳ����</returns>
        string[] GetCurrentPage();
        /// <summary>
        /// ������һҳ����������ǩ��״̬��
        /// </summary>
        void NextPage();

        /// <summary>
        /// ������һҳ����������ǩ��״̬��
        /// </summary>
        void PrevPage();

        void NextLine();
    }
}
